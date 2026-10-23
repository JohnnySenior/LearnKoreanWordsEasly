using System.Diagnostics;
using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lugat.Application.Ai;

public sealed record AiCallResult(AiJsonResponse Response, AiProviderKind Provider);

/// <summary>
/// Picks the AI service for a learner (their choice in settings, else the server default), falls back to the other
/// service when the first is unavailable, enforces the daily limit and records every attempt (spec BUD03, BUD05, REL01).
/// </summary>
public sealed class AiGateway(
    IAppDbContext db,
    IEnumerable<IAiProvider> providers,
    IOptions<AiOptions> options,
    TimeProvider time,
    ILogger<AiGateway> logger,
    AiUsageService usage,
    Premium.PremiumService premium)
{
    private AiOptions Options => options.Value;

    public async Task<AiStatusDto> StatusAsync(Guid ownerId, CancellationToken ct)
    {
        var chosen = await db.Users.Where(u => u.Id == ownerId).Select(u => u.AiProvider).SingleAsync(ct);
        var list = new List<AiProviderStatusDto>();
        foreach (var p in providers.OrderBy(p => p.Kind))
        {
            // A key being set is not a working connection: the status shows how the latest real call ended.
            var last = p.IsConfigured
                ? await db.AiUsages.AsNoTracking()
                    .Where(u => u.Provider == p.Kind && u.Feature != Speech.SpeechService.Feature && u.Feature != Speech.SpeakingService.Feature
                        && (u.Succeeded || u.Error != null))
                    .OrderByDescending(u => u.OccurredAt)
                    .Select(u => new { u.Succeeded, u.Error, u.OccurredAt })
                    .FirstOrDefaultAsync(ct)
                : null;
            list.Add(new AiProviderStatusDto(p.Kind, Label(p.Kind), p.IsConfigured, p.IsConfigured ? p.Model : null,
                last?.Succeeded, last is { Succeeded: false } ? last.Error : null, last?.OccurredAt));
        }

        var effective = Order(chosen).FirstOrDefault(p => p.IsConfigured)?.Kind;
        return new AiStatusDto(list, chosen, Options.DefaultProvider, effective, await CallsTodayAsync(ownerId, ct), await DailyLimitAsync(ownerId, ct));
    }

    public async Task<AiStatusDto> ChooseAsync(Guid ownerId, AiProviderKind? provider, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == ownerId, ct);
        user.AiProvider = provider;
        await db.SaveChangesAsync(ct);
        return await StatusAsync(ownerId, ct);
    }

    /// <summary>Calls the chosen service, or only <paramref name="only"/> when given (used by "check the connection").</summary>
    public async Task<AiCallResult> CompleteJsonAsync(Guid ownerId, AiJsonRequest request, CancellationToken ct, AiProviderKind? only = null)
    {
        // Every AI call costs money (spec §7.4): Premium learners, editors and admins only.
        if (!await premium.CanUseAiAsync(ownerId, ct))
        {
            throw new PremiumRequiredException();
        }

        var limit = await DailyLimitAsync(ownerId, ct);
        var chosen = await db.Users.Where(u => u.Id == ownerId).Select(u => u.AiProvider).SingleAsync(ct);
        var candidates = only is { } single
            ? providers.Where(p => p.Kind == single).ToList()
            : Order(chosen).Where(p => p.IsConfigured).ToList();
        if (!Options.FallbackToOther && only is null)
        {
            candidates = candidates.Take(1).ToList();
        }

        if (candidates.Count == 0 || candidates.All(p => !p.IsConfigured))
        {
            throw new ServiceUnavailableException("AI xizmati hali ulanmagan: serverda API kaliti yo‘q. Johnny Railway sozlamalariga kalit qo‘shishi kerak.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(Options.TotalTimeout);
        string? lastError = null;
        foreach (var provider in candidates.Where(p => p.IsConfigured))
        {
            if (deadline.IsCancellationRequested) break;
            // The reservation is persisted under a DB lock before contacting a billable provider.
            var output = provider.Kind == AiProviderKind.OpenAi ? Math.Max(request.MaxOutputTokens * 4, 8000) : request.MaxOutputTokens;
            var input = request.Instructions.Length + request.UserContent.Length + (request.Attachments?.Sum(a => a.Content.Length) ?? 0);
            AiUsage reservation;
            try
            {
                reservation = await usage.ReserveAsync(ownerId, request.Feature, provider.Kind, provider.Model, input, output, limit, deadline.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                lastError = "javob kechikdi";
                break;
            }
            var watch = Stopwatch.StartNew();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            timeout.CancelAfter(TimeSpan.FromTicks(Math.Min((request.Timeout ?? Options.Timeout).Ticks, Options.TotalTimeout.Ticks / Math.Max(1, candidates.Count))));
            try
            {
                var response = await provider.CompleteJsonAsync(request, timeout.Token);
                await usage.CompleteAsync(reservation, response.Model, response.InputTokens, response.OutputTokens, (int)watch.ElapsedMilliseconds, null);
                return new AiCallResult(response, provider.Kind);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await usage.FailAsync(reservation, (int)watch.ElapsedMilliseconds, "so‘rov bekor qilindi");
                throw;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Any failure of one service (error, timeout, unexpected response shape) moves on to the next one.
                lastError = ex switch
                {
                    AiProviderException => ex.Message,
                    OperationCanceledException or HttpRequestException => "javob kechikdi yoki aloqa uzildi",
                    _ => "kutilmagan javob",
                };
                // The exception type only: a message can quote the request, e.g. an invalid header value (the key).
                logger.LogWarning("AI call {Feature} via {Provider} failed: {Error} ({ErrorType})", request.Feature, provider.Kind, lastError, ex.GetType().Name);
                await usage.FailAsync(reservation, (int)watch.ElapsedMilliseconds, lastError);
            }
        }

        throw new ServiceUnavailableException($"AI xizmati hozir javob bermadi ({lastError}). Yozganingiz saqlandi, birozdan keyin qayta urinib ko‘ring.");
    }

    private async Task<int> DailyLimitAsync(Guid ownerId, CancellationToken ct)
    {
        var roles = await db.Users.Where(u => u.Id == ownerId).Select(u => u.Roles).SingleAsync(ct);
        return (roles & (Domain.Users.UserRoles.Admin | Domain.Users.UserRoles.Editor)) != 0 ? Options.MaxCallsPerEditorPerDay : Options.MaxCallsPerUserPerDay;
    }

    public static string Label(AiProviderKind kind) => kind == AiProviderKind.Anthropic ? "Claude (Anthropic)" : "ChatGPT (OpenAI)";

    private IEnumerable<IAiProvider> Order(AiProviderKind? chosen)
    {
        var first = chosen ?? Options.DefaultProvider;
        return providers.OrderBy(p => p.Kind == first ? 0 : 1).ThenBy(p => p.Kind);
    }

    private async Task<int> CallsTodayAsync(Guid ownerId, CancellationToken ct)
    {
        var since = time.GetUtcNow().AddDays(-1);
        return await db.AiUsages.CountAsync(u => u.OwnerId == ownerId && u.OccurredAt >= since && u.Feature != Speech.SpeechService.Feature && u.Feature != Speech.SpeakingService.Feature, ct); // speech has its own limits
    }

}
