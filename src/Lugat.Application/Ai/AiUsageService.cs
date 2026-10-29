using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Lugat.Application.Ai;

/// <summary>Rates must come from dated provider billing/configuration; missing rates are shown as unknown.</summary>
public sealed class AiBudgetOptions
{
    public decimal MonthlyLimitUsd { get; set; } = 25;
    public decimal WarningUsd { get; set; } = 20;
    public decimal HostingMonthlyUsd { get; set; }

    /// <summary>
    /// The hard stop for calls whose price is not configured (spec §13.9 "hard spend limit"): an unknown price cannot
    /// be added to the dollar budget, so such calls are capped by count per month instead. 0 = none allowed.
    /// </summary>
    public int MonthlyUnpricedCalls { get; set; } = 2000;
    public List<AiPrice> Prices { get; set; } = [];
}

public sealed class AiPrice
{
    public AiProviderKind Provider { get; set; }
    public string Model { get; set; } = "";
    public string Unit { get; set; } = "tokens";
    public decimal InputPerMillionUsd { get; set; }
    public decimal OutputPerMillionUsd { get; set; }
    public DateOnly AsOf { get; set; }
}

/// <summary>Reservations count towards quotas and the shared monthly budget before a billable call begins.</summary>
public sealed class AiUsageService(IAppDbContext db, IOperationLock operations, IOptions<AiBudgetOptions> options, TimeProvider time)
{
    /// <summary>The <see cref="AiUsage.Error"/> of a reservation whose call has not ended yet.</summary>
    public const string Pending = "pending";

    public async Task<AiUsage> ReserveAsync(Guid ownerId, string feature, AiProviderKind provider, string model,
        int input, int output, int dailyLimit, CancellationToken ct, string unit = "tokens")
    {
        await using var lease = await operations.AcquireAsync("ai-budget", ct);
        var since = time.GetUtcNow().AddDays(-1);
        var rows = db.AiUsages.Where(u => u.OwnerId == ownerId && u.OccurredAt >= since);
        rows = feature is "tts" or "stt" ? rows.Where(u => u.Feature == feature) : rows.Where(u => u.Feature != "tts" && u.Feature != "stt");
        if (await rows.CountAsync(ct) >= dailyLimit)
            throw new TooManyRequestsException($"Oxirgi 24 soatdagi so‘rovlar chegarasi tugadi ({dailyLimit} ta). Eski so‘rovlar 24 soatdan o‘tgach yana urinishingiz mumkin.");
        if (feature == "ping" && await db.AiUsages.AnyAsync(u => u.OwnerId == ownerId && u.Feature == "ping" && u.Provider == provider && u.OccurredAt >= time.GetUtcNow().AddMinutes(-1), ct))
            throw new TooManyRequestsException("Ulanish hozirgina tekshirildi. Bir daqiqadan keyin qayta urinib ko‘ring.");

        var rate = options.Value.Prices.FirstOrDefault(p => p.Provider == provider && p.Model == model && p.Unit == unit && p.AsOf != default);
        var cost = rate is null ? (decimal?)null : (input * rate.InputPerMillionUsd + output * rate.OutputPerMillionUsd) / 1_000_000m;
        var status = await BudgetAsync(ct);
        if (status.EstimatedUsd + (cost ?? 0) > status.LimitUsd || (cost is null && status.UnknownPriceCalls >= options.Value.MonthlyUnpricedCalls))
            throw new TooManyRequestsException("Oylik xarajat chegarasiga yetildi. Tayyor materiallar ishlaydi; limitni server sozlamalarida ongli ravishda oshirish mumkin.");

        var row = new AiUsage
        {
            Id = Guid.CreateVersion7(time.GetUtcNow()),
            OwnerId = ownerId,
            Feature = feature,
            Provider = provider,
            Model = model,
            InputTokens = input,
            OutputTokens = output,
            Unit = unit,
            EstimatedUsd = cost,
            PriceAsOf = rate?.AsOf,
            Error = Pending,
            OccurredAt = time.GetUtcNow(),
        };
        db.AiUsages.Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }

    public async Task CompleteAsync(AiUsage row, string model, int input, int output, int duration, string? error)
    {
        // Scale against the reservation's price snapshot, not a newly edited rate.
        var rate = options.Value.Prices.FirstOrDefault(p => p.Provider == row.Provider && p.Model == row.Model && p.Unit == row.Unit && p.AsOf == row.PriceAsOf);
        row.EstimatedUsd = rate is null ? null : (input * rate.InputPerMillionUsd + output * rate.OutputPerMillionUsd) / 1_000_000m;
        row.Model = model.Length > 128 ? model[..128] : model;
        row.InputTokens = input;
        row.OutputTokens = output;
        row.DurationMs = duration;
        row.Succeeded = error is null;
        row.Error = error;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    public async Task FailAsync(AiUsage row, int duration, string error)
    {
        // A timed-out provider may have charged us. Keep the conservative reservation rather than pretending $0.
        row.Succeeded = false;
        row.Error = error.Length > 500 ? error[..500] : error;
        row.DurationMs = duration;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Reject unusable content without refunding tokens, costs, or the provider's abuse limits.</summary>
    public async Task RejectContentAsync(Guid usageId, string reason)
    {
        var row = await db.AiUsages.SingleAsync(u => u.Id == usageId, CancellationToken.None);
        row.Succeeded = false;
        row.Error = reason.Length > 500 ? reason[..500] : reason;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    public async Task<AiBudgetDto> BudgetAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var month = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var rows = db.AiUsages.Where(u => u.OccurredAt >= month);
        var total = (await rows.SumAsync(u => u.EstimatedUsd, ct) ?? 0) + options.Value.HostingMonthlyUsd;
        var unknown = await rows.CountAsync(u => u.EstimatedUsd == null, ct);
        return new AiBudgetDto(total, options.Value.MonthlyLimitUsd, total >= options.Value.WarningUsd,
            unknown, await rows.CountAsync(u => u.Error == Pending, ct), month);
    }
}
