using System.Security.Cryptography;
using Lugat.Application.Abstractions;
using Lugat.Contracts;
using Lugat.Domain.Premium;
using Lugat.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lugat.Application.Premium;

public sealed class PremiumOptions
{
    public const string SectionName = "Premium";

    /// <summary>Prices live in configuration, never in code (spec §7.2).</summary>
    public List<PlanOption> Plans { get; set; } =
    [
        new() { Plan = SubscriptionPlan.Monthly, PriceKrw = 5000, PriceUzs = 29000, Months = 1 },
        new() { Plan = SubscriptionPlan.Quarterly, PriceKrw = 12000, PriceUzs = 69000, Months = 3 },
    ];

    /// <summary>Free Premium for a new learner (spec §7.2, Q3): 0 = off.</summary>
    public int TrialDays { get; set; }

    /// <summary>
    /// L-64: free Premium the learner starts themselves after passing their first unit, once per learner and only if
    /// they never had a trial (the signup one included). 0 = off.
    /// </summary>
    public int EarnedTrialDays { get; set; } = 3;

    /// <summary>The catalog's checked trap note is free (spec §7.1, Q2).</summary>
    public bool TrapNotesFree { get; set; } = true;
}

public sealed class PlanOption
{
    public SubscriptionPlan Plan { get; set; }

    public int PriceKrw { get; set; }

    /// <summary>The price paid from Uzbekistan, in so‘m (L-48): set on its own, lower than the won price at launch.</summary>
    public int PriceUzs { get; set; }

    public int Months { get; set; }

    public int Price(Currency currency) => currency == Currency.Uzs ? PriceUzs : PriceKrw;
}

/// <summary>How to pay by bank transfer (spec §7.3). Empty until Johnny fills it in (Q4).</summary>
public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    public string? BankName { get; set; }

    public string? AccountNumber { get; set; }

    public string? AccountHolder { get; set; }

    /// <summary>Telegram username for receipts, without "@".</summary>
    public string? ContactTelegram { get; set; }

    /// <summary>The card for paying in so‘m from Uzbekistan (L-48); <c>Payments:Uz:*</c>.</summary>
    public PaymentAccount Uz { get; set; } = new();
}

public sealed class PaymentAccount
{
    public string? BankName { get; set; }

    public string? AccountNumber { get; set; }

    public string? AccountHolder { get; set; }

    /// <summary>"Humo" or "Uzcard".</summary>
    public string? Kind { get; set; }
}

/// <summary>
/// A payment provider (Toss Payments, PortOne) for a later version (spec §7.3). v1 has none: payments are bank
/// transfers recorded by an admin, and a gateway will create the same <see cref="Payment"/> and <see cref="Subscription"/>.
/// </summary>
public interface IPaymentGateway
{
    string Name { get; }
}

/// <summary>
/// Premium (spec §7): who has it, plans and promo codes, and the admin's manual payment flow. Only the server
/// decides (spec §0.6): Premium features answer 403 "premium-required" without it.
/// </summary>
public sealed class PremiumService(
    IAppDbContext db,
    TimeProvider time,
    IOperationLock operations,
    ITelegramMessenger telegram,
    IOptionsMonitor<PremiumOptions> premium,
    IOptionsMonitor<PaymentOptions> payments,
    ILogger<PremiumService> logger)
{
    private const string CodeAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ"; // no 0/O, 1/I/L

    public async Task<bool> IsPremiumAsync(Guid userId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return await db.Subscriptions.AnyAsync(s => s.UserId == userId && s.StartsAt <= now && s.EndsAt > now, ct);
    }

    /// <exception cref="PremiumRequiredException">The learner has no Premium now.</exception>
    public async Task RequireAsync(Guid userId, CancellationToken ct)
    {
        if (!await IsPremiumAsync(userId, ct))
        {
            throw new PremiumRequiredException();
        }
    }

    /// <summary>
    /// AI costs money on every call (spec §7.4): Premium learners may use it; editors and admins too, for the shared
    /// content (AI drafts) and for checking the services.
    /// </summary>
    public async Task<bool> CanUseAiAsync(Guid userId, CancellationToken ct)
    {
        var roles = await db.Users.Where(u => u.Id == userId).Select(u => u.Roles).SingleOrDefaultAsync(ct);
        return (roles & (UserRoles.Admin | UserRoles.Editor)) != 0 || await IsPremiumAsync(userId, ct);
    }

    public async Task<PremiumStatusDto> StatusAsync(Guid userId, CancellationToken ct)
    {
        var code = await PaymentCodeAsync(userId, ct);
        var now = time.GetUtcNow();
        var running = await db.Subscriptions.Where(s => s.UserId == userId && s.EndsAt > now).OrderBy(s => s.StartsAt).ToListAsync(ct);
        var current = running.FirstOrDefault(s => s.StartsAt <= now);
        return new PremiumStatusDto(current is not null, current is null ? null : running.Max(s => s.EndsAt), current?.Plan, code, premium.CurrentValue.TrapNotesFree,
            await Teaching.GroupService.AttachedPromoAsync(db, userId, now, ct));
    }

    /// <summary>
    /// L-64: the earned trial is offered when it is on, the learner has no Premium now, never had a trial (the signup
    /// trial included) and passed at least one unit.
    /// </summary>
    public async Task<TrialOfferDto> TrialOfferAsync(Guid userId, CancellationToken ct)
    {
        var days = premium.CurrentValue.EarnedTrialDays;
        return new TrialOfferDto(days > 0 && await TrialProblemAsync(userId, ct) is null, Math.Max(0, days));
    }

    /// <summary>
    /// L-64: starts the earned trial. A second tap while it runs returns the same status; otherwise a learner who may
    /// not start it gets the reason (409).
    /// </summary>
    public async Task<PremiumStatusDto> StartTrialAsync(Guid userId, CancellationToken ct)
    {
        var days = premium.CurrentValue.EarnedTrialDays;
        await using var lease = await operations.AcquireAsync("premium:" + userId, ct);
        var now = time.GetUtcNow();
        if (await db.Subscriptions.AnyAsync(s => s.UserId == userId && s.Plan == SubscriptionPlan.Trial && s.StartsAt <= now && s.EndsAt > now, ct))
        {
            return await StatusAsync(userId, ct);
        }

        if (days <= 0)
        {
            throw new ConflictException("Sinov muddati hozir yo‘q.");
        }

        if (await TrialProblemAsync(userId, ct) is { } problem)
        {
            throw new ConflictException(problem);
        }

        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            Plan = SubscriptionPlan.Trial,
            StartsAt = now,
            EndsAt = now.AddDays(days),
            Source = SubscriptionSource.Promo,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Earned trial started for {UserId}: {Days} days", userId, days);
        return await StatusAsync(userId, ct);
    }

    /// <summary>L-64: counts one Premium page view for a known feature key; anything else is ignored.</summary>
    public async Task CountViewAsync(string? feature, CancellationToken ct)
    {
        if (feature is null || !PremiumFeatures.All.Contains(feature))
        {
            return;
        }

        var day = Domain.KoreaTime.Day(time.GetUtcNow());
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (await db.PremiumViews.Where(v => v.Feature == feature && v.Day == day).ExecuteUpdateAsync(v => v.SetProperty(x => x.Count, x => x.Count + 1), ct) > 0)
            {
                return;
            }

            db.PremiumViews.Add(new PremiumView { Feature = feature, Day = day, Count = 1 });
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException)
            {
                // Another view made today's row first: count on it.
                db.DiscardChanges();
            }
        }
    }

    private async Task<string?> TrialProblemAsync(Guid userId, CancellationToken ct)
    {
        if (await db.Subscriptions.AnyAsync(s => s.UserId == userId && s.Plan == SubscriptionPlan.Trial, ct))
        {
            return "Sinov muddatidan avval foydalangansiz.";
        }

        if (await IsPremiumAsync(userId, ct))
        {
            return "Premium allaqachon faol.";
        }

        return await db.UnitProgress.AnyAsync(p => p.UserId == userId && p.Status == Domain.Catalog.UnitProgressStatus.Passed, ct)
            ? null
            : "Sinov birinchi bo‘lim testidan keyin ochiladi.";
    }

    public PremiumPlansDto Plans()
    {
        var p = payments.CurrentValue;
        var bank = string.IsNullOrWhiteSpace(p.BankName) || string.IsNullOrWhiteSpace(p.AccountNumber) || string.IsNullOrWhiteSpace(p.AccountHolder)
            ? null
            : new BankDetailsDto(p.BankName, p.AccountNumber, p.AccountHolder);
        var uz = p.Uz;
        var plans = premium.CurrentValue.Plans;
        // The so‘m card shows only when it is complete and every plan has a so‘m price.
        var bankUz = string.IsNullOrWhiteSpace(uz.BankName) || string.IsNullOrWhiteSpace(uz.AccountNumber) || string.IsNullOrWhiteSpace(uz.AccountHolder)
                || plans.Any(x => x.PriceUzs <= 0)
            ? null
            : new BankDetailsDto(uz.BankName, uz.AccountNumber, uz.AccountHolder, string.IsNullOrWhiteSpace(uz.Kind) ? null : uz.Kind.Trim());
        return new PremiumPlansDto(
            plans.Select(x => new PlanDto(x.Plan, Title(x), x.PriceKrw, x.Months, x.PriceUzs)).ToList(),
            bank,
            string.IsNullOrWhiteSpace(p.ContactTelegram) ? null : p.ContactTelegram.Trim().TrimStart('@'),
            premium.CurrentValue.TrialDays,
            bankUz);
    }

    /// <summary>The discount shows at once (spec §7.3 step 1); an unknown, inactive or expired code says why.</summary>
    public async Task<PromoCheckDto> CheckPromoAsync(PromoCheckRequest request, CancellationToken ct)
    {
        var plan = Plan(request.Plan, request.Currency);
        var price = plan.Price(request.Currency);
        var code = PromoCode.Normalize(request.Code ?? "");
        var (promo, problem) = await FindPromoAsync(code, ct);
        return promo is null
            ? new PromoCheckDto(false, code, 0, price, price, problem, request.Currency)
            : new PromoCheckDto(true, promo.Code, promo.DiscountPercent, price, PremiumMath.Discounted(price, promo.DiscountPercent), null, request.Currency);
    }

    /// <summary>Finds learners by name, payment code ("L-7K3Q", "l7k3q"), Telegram username or email (spec §10).</summary>
    public async Task<IReadOnlyList<AdminUserDto>> SearchUsersAsync(string? query, CancellationToken ct)
    {
        var q = (query ?? "").Trim();
        if (q.Length < 2)
        {
            return [];
        }

        var code = NormalizePaymentCode(q);
        var pattern = "%" + q.ToLowerInvariant().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        var users = await db.Users
            .Where(u => u.DeletedAt == null && (u.PaymentCode == code
                || (u.DisplayName != null && EF.Functions.Like(u.DisplayName.ToLower(), pattern))
                || db.UserLogins.Any(l => l.UserId == u.Id && l.Display != null && EF.Functions.Like(l.Display.ToLower(), pattern))))
            .OrderBy(u => u.DisplayName).Take(20).ToListAsync(ct);
        return await AdminUsersAsync(users, ct);
    }

    /// <summary>
    /// Records a checked bank transfer (spec §7.3 step 3): the payment and a subscription that starts where the
    /// learner's current Premium ends, so paying twice adds up. Recording the same payment again changes nothing.
    /// </summary>
    public async Task<PaymentDto> RecordPaymentAsync(Guid adminId, RecordPaymentRequest request, CancellationToken ct)
    {
        var plan = Plan(request.Plan, request.Currency);
        if (request.Amount <= 0)
        {
            throw new ValidationException("To‘langan summani kiriting.");
        }

        await using var lease = await operations.AcquireAsync("premium:" + request.UserId, ct);
        if (await db.Payments.SingleOrDefaultAsync(p => p.Id == request.Id, ct) is { } existing)
        {
            return existing.UserId == request.UserId ? await PaymentDtoAsync(existing, ct) : throw new ConflictException("Bu to‘lov boshqa foydalanuvchiga yozilgan.");
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == request.UserId && u.DeletedAt == null, ct) ?? throw new NotFoundException("Foydalanuvchi topilmadi.");
        PromoCode? promo = null;
        if (!string.IsNullOrWhiteSpace(request.PromoCode))
        {
            (promo, var problem) = await FindPromoAsync(PromoCode.Normalize(request.PromoCode), ct);
            if (promo is null)
            {
                throw new ValidationException(problem!);
            }
        }

        var now = time.GetUtcNow();
        var ends = await db.Subscriptions.Where(s => s.UserId == user.Id && s.EndsAt > now).Select(s => s.EndsAt).ToListAsync(ct);
        var start = PremiumMath.NextStart(now, ends);
        var payment = new Payment
        {
            Id = request.Id,
            UserId = user.Id,
            Amount = request.Amount,
            Currency = request.Currency,
            ExpectedAmount = PremiumMath.Discounted(plan.Price(request.Currency), promo?.DiscountPercent ?? 0),
            Plan = plan.Plan,
            PaidAt = request.PaidAt ?? now,
            Method = PaymentMethod.BankTransfer,
            PromoCodeId = promo?.Id,
            DiscountPercent = promo?.DiscountPercent ?? 0,
            Commission = promo?.TeacherUserId is null ? 0 : PremiumMath.Commission(request.Amount, promo.CommissionPercent),
            Note = Cut(request.Note, 500),
            RecordedByUserId = adminId,
            Status = PaymentStatus.Confirmed,
            CreatedAt = now,
        };
        db.Payments.Add(payment);
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.CreateVersion7(now),
            UserId = user.Id,
            Plan = plan.Plan,
            StartsAt = start,
            EndsAt = start.AddMonths(plan.Months),
            Source = SubscriptionSource.ManualTransfer,
            PaymentId = payment.Id,
            CreatedByUserId = adminId,
            CreatedAt = now,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.DiscardChanges();
            return await PaymentDtoAsync(await db.Payments.SingleAsync(p => p.Id == request.Id, ct), ct);
        }

        var dto = await PaymentDtoAsync(payment, ct);
        await NotifyAsync(user.Id, $"Premium faollashdi. Muddati: {dto.EndsAt:dd.MM.yyyy} gacha. Rahmat!", ct);
        return dto;
    }

    /// <summary>
    /// A refund (spec §7.3 step 5): the payment's period ends today (or is dropped if it had not started), and periods
    /// queued after it move up by the time taken away, so the learner keeps exactly what else they paid for.
    /// </summary>
    public async Task<PaymentDto> RefundAsync(Guid paymentId, RefundPaymentRequest request, CancellationToken ct)
    {
        var userId = await db.Payments.Where(p => p.Id == paymentId).Select(p => (Guid?)p.UserId).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("To‘lov topilmadi.");
        await using var lease = await operations.AcquireAsync("premium:" + userId, ct);
        var payment = await db.Payments.SingleAsync(p => p.Id == paymentId, ct);
        if (payment.Status == PaymentStatus.Refunded)
        {
            return await PaymentDtoAsync(payment, ct);
        }

        var now = time.GetUtcNow();
        var subscription = await db.Subscriptions.SingleOrDefaultAsync(s => s.PaymentId == paymentId, ct);
        if (subscription is not null && subscription.EndsAt > now)
        {
            var oldEnd = subscription.EndsAt;
            var newEnd = subscription.StartsAt > now ? subscription.StartsAt : now;
            var removed = oldEnd - newEnd;
            subscription.EndsAt = newEnd;
            var later = await db.Subscriptions.Where(s => s.UserId == payment.UserId && s.Id != subscription.Id && s.StartsAt >= oldEnd).ToListAsync(ct);
            foreach (var s in later)
            {
                (s.StartsAt, s.EndsAt) = (s.StartsAt - removed, s.EndsAt - removed);
            }
        }

        (payment.Status, payment.RefundedAt, payment.RefundNote) = (PaymentStatus.Refunded, now, Cut(request.Note, 500));
        await db.SaveChangesAsync(ct);
        return await PaymentDtoAsync(payment, ct);
    }

    public async Task<IReadOnlyList<PaymentDto>> PaymentsAsync(Guid? userId, CancellationToken ct)
    {
        var list = await db.Payments.Where(p => userId == null || p.UserId == userId).OrderByDescending(p => p.PaidAt).Take(100).ToListAsync(ct);
        return await PaymentDtosAsync(list, ct);
    }

    public async Task<PromoCodeDto> CreatePromoAsync(CreatePromoCodeRequest request, CancellationToken ct)
    {
        var code = PromoCode.Normalize(request.Code ?? "");
        if (code.Length is < 3 or > 32 || !code.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            throw new ValidationException("Promo-kod 3–32 belgi: lotin harflari, raqamlar va «-».");
        }

        if (request.DiscountPercent is < 0 or > 100 || request.CommissionPercent is < 0 or > 100)
        {
            throw new ValidationException("Chegirma va komissiya 0–100% bo‘lsin.");
        }

        if (request.TeacherUserId is { } teacher && !await db.Users.AnyAsync(u => u.Id == teacher && u.DeletedAt == null, ct))
        {
            throw new ValidationException("O‘qituvchi topilmadi.");
        }

        var promo = new PromoCode
        {
            Id = Guid.CreateVersion7(time.GetUtcNow()),
            Code = code,
            DiscountPercent = request.DiscountPercent,
            CommissionPercent = request.CommissionPercent ?? 30,
            TeacherUserId = request.TeacherUserId,
            ValidUntil = request.ValidUntil,
            CreatedAt = time.GetUtcNow(),
        };
        db.PromoCodes.Add(promo);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.DiscardChanges();
            throw new ConflictException("Bunday promo-kod allaqachon bor.");
        }

        return (await PromoDtosAsync([promo], ct))[0];
    }

    public async Task<IReadOnlyList<PromoCodeDto>> PromoCodesAsync(CancellationToken ct) =>
        await PromoDtosAsync(await db.PromoCodes.OrderByDescending(p => p.CreatedAt).ToListAsync(ct), ct);

    private PlanOption Plan(SubscriptionPlan plan, Currency currency) =>
        Enum.IsDefined(currency)
            ? premium.CurrentValue.Plans.FirstOrDefault(p => p.Plan == plan && p.Months > 0 && p.Price(currency) > 0)
                ?? throw new ValidationException(currency == Currency.Uzs ? "Bu tarifning so‘mdagi narxi yo‘q." : "Bunday tarif yo‘q.")
            : throw new ValidationException("Valyuta noto‘g‘ri.");

    private static string Title(PlanOption p) => p.Months == 1 ? "1 oy" : $"{p.Months} oy";

    private async Task<(PromoCode? Promo, string? Problem)> FindPromoAsync(string code, CancellationToken ct)
    {
        if (code.Length == 0)
        {
            return (null, "Promo-kodni kiriting.");
        }

        var promo = await db.PromoCodes.SingleOrDefaultAsync(p => p.Code == code, ct);
        return promo switch
        {
            null => (null, "Bunday promo-kod topilmadi."),
            { IsActive: false } => (null, "Bu promo-kod endi amal qilmaydi."),
            { ValidUntil: { } until } when until <= time.GetUtcNow() => (null, "Bu promo-kodning muddati tugagan."),
            _ => (promo, null),
        };
    }

    /// <summary>The learner's payment code, made once (unique; retried on the rare clash).</summary>
    private async Task<string> PaymentCodeAsync(Guid userId, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
            if (user.PaymentCode is { } code)
            {
                return code;
            }

            user.PaymentCode = "L-" + RandomNumberGenerator.GetString(CodeAlphabet, 4);
            try
            {
                await db.SaveChangesAsync(ct);
                return user.PaymentCode;
            }
            catch (DbUpdateException) when (attempt < 5)
            {
                db.DiscardChanges();
            }
        }
    }

    public static string NormalizePaymentCode(string text)
    {
        var raw = new string(text.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        return raw.Length == 5 && raw[0] == 'L' ? "L-" + raw[1..] : text.Trim().ToUpperInvariant();
    }

    private async Task NotifyAsync(Guid userId, string text, CancellationToken ct)
    {
        // In the app the Premium page shows it at once; the bot message is a courtesy (spec §7.3 step 4).
        var chat = await db.UserLogins.Where(l => l.UserId == userId && l.Provider == LoginProvider.Telegram).Select(l => l.ProviderUserId).FirstOrDefaultAsync(ct);
        if (chat is null || !telegram.IsConfigured || !long.TryParse(chat, out var chatId))
        {
            return;
        }

        try
        {
            await telegram.SendAsync(chatId, text, "Premium", ct, "premium");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Premium message to the learner failed: {Error}", ex.GetType().Name);
        }
    }

    private async Task<IReadOnlyList<AdminUserDto>> AdminUsersAsync(List<User> users, CancellationToken ct)
    {
        var ids = users.Select(u => u.Id).ToList();
        var now = time.GetUtcNow();
        var logins = (await db.UserLogins.Where(l => ids.Contains(l.UserId)).Select(l => new { l.UserId, l.Provider, l.Display }).ToListAsync(ct))
            .ToLookup(l => l.UserId, l => $"{l.Provider}{(l.Display is null ? "" : $" {l.Display}")}");
        var subs = (await db.Subscriptions.Where(s => ids.Contains(s.UserId) && s.EndsAt > now).ToListAsync(ct)).ToLookup(s => s.UserId);
        return users.Select(u =>
        {
            var running = subs[u.Id].ToList();
            var on = running.Any(s => s.StartsAt <= now);
            return new AdminUserDto(u.Id, u.DisplayName, u.PaymentCode, logins[u.Id].ToList(), on, on ? running.Max(s => s.EndsAt) : null);
        }).ToList();
    }

    private async Task<PaymentDto> PaymentDtoAsync(Payment p, CancellationToken ct) => (await PaymentDtosAsync([p], ct))[0];

    /// <summary>Subscriptions, names and promo codes of all the payments in three queries, not three per payment (audit 2026-10-26).</summary>
    private async Task<List<PaymentDto>> PaymentDtosAsync(IReadOnlyList<Payment> payments, CancellationToken ct)
    {
        var paymentIds = payments.Select(p => (Guid?)p.Id).ToList();
        var subs = (await db.Subscriptions.Where(s => paymentIds.Contains(s.PaymentId)).Select(s => new { s.PaymentId, s.StartsAt, s.EndsAt }).ToListAsync(ct))
            .ToDictionary(s => s.PaymentId!.Value);
        var userIds = payments.SelectMany(p => new[] { p.UserId, p.RecordedByUserId }).Distinct().ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var promoIds = payments.Select(p => p.PromoCodeId).OfType<Guid>().Distinct().ToList();
        var promos = promoIds.Count == 0 ? [] : await db.PromoCodes.Where(x => promoIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, ct);
        return payments.Select(p =>
        {
            var sub = subs.GetValueOrDefault(p.Id);
            var promo = p.PromoCodeId is { } id ? promos.GetValueOrDefault(id) : null;
            return new PaymentDto(p.Id, p.UserId, names.GetValueOrDefault(p.UserId), p.Plan, p.Amount, p.ExpectedAmount, promo, p.DiscountPercent,
                p.Commission, p.Status, p.PaidAt, sub?.StartsAt ?? p.PaidAt, sub?.EndsAt ?? p.PaidAt, p.Note, names.GetValueOrDefault(p.RecordedByUserId), p.Currency);
        }).ToList();
    }

    private async Task<List<PromoCodeDto>> PromoDtosAsync(List<PromoCode> promos, CancellationToken ct)
    {
        var ids = promos.Select(p => (Guid?)p.Id).ToList();
        var teachers = promos.Select(p => p.TeacherUserId).OfType<Guid>().ToList();
        var names = await db.Users.Where(u => teachers.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var counts = await db.Payments.Where(p => ids.Contains(p.PromoCodeId) && p.Status == PaymentStatus.Confirmed)
            .GroupBy(p => p.PromoCodeId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key!.Value, x => x.Count, ct);
        return promos.Select(p => new PromoCodeDto(p.Id, p.Code, p.DiscountPercent, p.CommissionPercent,
            p.TeacherUserId is { } t ? names.GetValueOrDefault(t) ?? "—" : null, p.IsActive, p.ValidUntil, counts.GetValueOrDefault(p.Id))).ToList();
    }

    private static string? Cut(string? text, int max)
    {
        var t = text?.Trim();
        return string.IsNullOrEmpty(t) ? null : t.Length > max ? t[..max] : t;
    }
}
