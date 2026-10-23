using Lugat.Domain.Premium;

namespace Lugat.Contracts;

/// <param name="IsPremium">Decided by the server (spec §0.6); the client only shows it.</param>
/// <param name="EndsAt">The end of the last paid or given period, when Premium is on.</param>
/// <param name="PaymentCode">To write in the bank transfer note (spec §7.3).</param>
public sealed record PremiumStatusDto(bool IsPremium, DateTimeOffset? EndsAt, SubscriptionPlan? Plan, string PaymentCode, bool TrapNotesFree);

public sealed record PlanDto(SubscriptionPlan Plan, string Title, int PriceKrw, int Months);

/// <param name="Bank">Null until the bank details are configured: the page says "Tez orada" (spec §15 Q4).</param>
/// <param name="ContactTelegram">Where to send the receipt, without "@".</param>
public sealed record PremiumPlansDto(IReadOnlyList<PlanDto> Plans, BankDetailsDto? Bank, string? ContactTelegram, int TrialDays);

public sealed record BankDetailsDto(string BankName, string AccountNumber, string AccountHolder);

public sealed record PromoCheckRequest(string Code, SubscriptionPlan Plan);

/// <param name="Message">Why the code does not work, in Uzbek.</param>
public sealed record PromoCheckDto(bool Valid, string Code, int DiscountPercent, int PriceKrw, int FinalPriceKrw, string? Message);

// Admin (spec §7.3, §10): find the payer, record the payment, refund.

public sealed record AdminUserDto(Guid Id, string? DisplayName, string? PaymentCode, IReadOnlyList<string> Logins, bool IsPremium, DateTimeOffset? PremiumEndsAt);

/// <param name="Id">Chosen by the page once; recording again returns the same payment (API01).</param>
/// <param name="PaidAt">When the money arrived; now when not given.</param>
public sealed record RecordPaymentRequest(Guid Id, Guid UserId, SubscriptionPlan Plan, int AmountKrw, string? PromoCode, DateTimeOffset? PaidAt, string? Note);

public sealed record PaymentDto(
    Guid Id,
    Guid UserId,
    string? UserName,
    SubscriptionPlan Plan,
    int AmountKrw,
    int ExpectedKrw,
    string? PromoCode,
    int DiscountPercent,
    int CommissionKrw,
    PaymentStatus Status,
    DateTimeOffset PaidAt,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? Note,
    string? RecordedBy);

public sealed record RefundPaymentRequest(string? Note);

public sealed record CreatePromoCodeRequest(string Code, int DiscountPercent, int? CommissionPercent, Guid? TeacherUserId, DateTimeOffset? ValidUntil);

public sealed record PromoCodeDto(Guid Id, string Code, int DiscountPercent, int CommissionPercent, string? Teacher, bool IsActive, DateTimeOffset? ValidUntil, int Payments);
