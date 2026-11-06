using Lugat.Domain.Premium;

namespace Lugat.Contracts;

/// <param name="IsPremium">Decided by the server (spec §0.6); the client only shows it.</param>
/// <param name="EndsAt">The end of the last paid or given period, when Premium is on.</param>
/// <param name="PaymentCode">To write in the bank transfer note (spec §7.3).</param>
/// <param name="PromoCode">The teacher's code the learner came with, filled in on the page (spec §9.1).</param>
public sealed record PremiumStatusDto(bool IsPremium, DateTimeOffset? EndsAt, SubscriptionPlan? Plan, string PaymentCode, bool TrapNotesFree, string? PromoCode = null);

/// <param name="PriceUzs">The price for learners paying from Uzbekistan, in so‘m (L-48).</param>
public sealed record PlanDto(SubscriptionPlan Plan, string Title, int PriceKrw, int Months, int PriceUzs = 0)
{
    public int Price(Currency currency) => currency == Currency.Uzs ? PriceUzs : PriceKrw;
}

/// <param name="Bank">Null until the bank details are configured: the page says "Tez orada" (spec §15 Q4).</param>
/// <param name="ContactTelegram">Where to send the receipt, without "@".</param>
/// <param name="BankUz">The card for paying in so‘m from Uzbekistan; null when not configured (L-48).</param>
public sealed record PremiumPlansDto(IReadOnlyList<PlanDto> Plans, BankDetailsDto? Bank, string? ContactTelegram, int TrialDays, BankDetailsDto? BankUz = null);

/// <param name="Kind">The card type ("Humo", "Uzcard") for a card; null for a bank account.</param>
public sealed record BankDetailsDto(string BankName, string AccountNumber, string AccountHolder, string? Kind = null);

public sealed record PromoCheckRequest(string Code, SubscriptionPlan Plan, Currency Currency = Currency.Krw);

/// <param name="Message">Why the code does not work, in Uzbek.</param>
public sealed record PromoCheckDto(bool Valid, string Code, int DiscountPercent, int Price, int FinalPrice, string? Message, Currency Currency = Currency.Krw);

// Admin (spec §7.3, §10): find the payer, record the payment, refund.

public sealed record AdminUserDto(Guid Id, string? DisplayName, string? PaymentCode, IReadOnlyList<string> Logins, bool IsPremium, DateTimeOffset? PremiumEndsAt);

/// <param name="Id">Chosen by the page once; recording again returns the same payment (API01).</param>
/// <param name="PaidAt">When the money arrived; now when not given.</param>
/// <param name="Amount">What arrived, in <paramref name="Currency"/>.</param>
public sealed record RecordPaymentRequest(Guid Id, Guid UserId, SubscriptionPlan Plan, int Amount, string? PromoCode, DateTimeOffset? PaidAt, string? Note, Currency Currency = Currency.Krw);

public sealed record PaymentDto(
    Guid Id,
    Guid UserId,
    string? UserName,
    SubscriptionPlan Plan,
    int Amount,
    int ExpectedAmount,
    string? PromoCode,
    int DiscountPercent,
    int Commission,
    PaymentStatus Status,
    DateTimeOffset PaidAt,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? Note,
    string? RecordedBy,
    Currency Currency = Currency.Krw);

public sealed record RefundPaymentRequest(string? Note);

public sealed record CreatePromoCodeRequest(string Code, int DiscountPercent, int? CommissionPercent, Guid? TeacherUserId, DateTimeOffset? ValidUntil);

public sealed record PromoCodeDto(Guid Id, string Code, int DiscountPercent, int CommissionPercent, string? Teacher, bool IsActive, DateTimeOffset? ValidUntil, int Payments);

/// <summary>L-64: how many words the learner missed lately, for the Premium page; never the words themselves.</summary>
public sealed record MistakeCountDto(int Count, int Days);

/// <summary>
/// L-64: which Premium feature a learner wanted, so the Premium page can say what it gives. A view key, not a rule:
/// whether the learner has Premium is decided by the server only (spec §0.6).
/// </summary>
public static class PremiumFeatures
{
    public const string Mistakes = "xatolar";
    public const string WeakPoints = "zaif";
    public const string Ladder = "zinapoya";
    public const string Traps = "tuzoqlar";
    public const string Listening = "tinglash";
    public const string Sentence = "gap";
    public const string Speaking = "talaffuz";

    public static readonly IReadOnlyList<string> All = [Mistakes, WeakPoints, Ladder, Traps, Listening, Sentence, Speaking];

    /// <summary>The feature a refused API path belongs to ("/api/mistakes" → "xatolar"); null when it is none of them.</summary>
    public static string? KeyOf(string? path)
    {
        var p = (path ?? "").Split('?', '#')[0].TrimEnd('/').ToLowerInvariant();
        static bool Under(string p, string root) => p == root || p.StartsWith(root + "/", StringComparison.Ordinal);
        return p switch
        {
            _ when Under(p, "/api/mistakes") => Mistakes,
            _ when Under(p, "/api/weak-points") => WeakPoints,
            _ when Under(p, "/api/ladders") => Ladder,
            _ when Under(p, "/api/traps") => Traps,
            _ when Under(p, "/api/listening") => Listening,
            _ when Under(p, "/api/practice") => Sentence,
            _ => null,
        };
    }
}

/// <summary>L-64: whether the learner may start the earned trial now, and for how many days.</summary>
public sealed record TrialOfferDto(bool Eligible, int Days);

/// <summary>L-64: a premium page opened for a feature (<see cref="PremiumFeatures"/> key), counted per day.</summary>
public sealed record PremiumViewRequest(string Feature);
