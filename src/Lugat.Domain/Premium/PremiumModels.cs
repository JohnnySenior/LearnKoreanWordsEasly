namespace Lugat.Domain.Premium;

public enum SubscriptionPlan
{
    Monthly,
    Quarterly,
    Trial,
    Gift,
}

public enum SubscriptionSource
{
    ManualTransfer,
    Promo,
    Gateway,
    Admin,
}

/// <summary>The money a payment is in (L-48): won in Korea, so‘m from Uzbekistan. Sums never mix currencies.</summary>
public enum Currency
{
    Krw,
    Uzs,
}

public enum PaymentMethod
{
    BankTransfer,
}

public enum PaymentStatus
{
    Confirmed,
    Refunded,
}

/// <summary>
/// A period of Premium (spec §5.1). Premium = a subscription with <c>StartsAt ≤ now &lt; EndsAt</c>. A new paid
/// period starts where the learner's last one ends, so two payments add up (spec §7.3).
/// </summary>
public sealed class Subscription
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public SubscriptionPlan Plan { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public SubscriptionSource Source { get; set; }

    public Guid? PaymentId { get; set; }

    /// <summary>The admin who recorded it; null when the system made it (trial).</summary>
    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public uint Version { get; set; }
}

/// <summary>
/// Money received (spec §5.1, §7.3), recorded by an admin after checking the bank transfer. Kept for bookkeeping
/// even when the learner deletes the account (the user row stays as an anonymous tombstone).
/// </summary>
public sealed class Payment
{
    /// <summary>Chosen by the admin page once, so a double click records one payment (API01).</summary>
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>What arrived, in <see cref="Currency"/>.</summary>
    public int Amount { get; set; }

    public Currency Currency { get; set; } = Currency.Krw;

    /// <summary>The plan price in <see cref="Currency"/> after the promo discount, at the time of recording.</summary>
    public int ExpectedAmount { get; set; }

    public SubscriptionPlan Plan { get; set; }

    public DateTimeOffset PaidAt { get; set; }

    public PaymentMethod Method { get; set; }

    public Guid? PromoCodeId { get; set; }

    public int DiscountPercent { get; set; }

    /// <summary>The teacher's share (spec §9.3) in <see cref="Currency"/>, when the promo code belongs to a teacher.</summary>
    public int Commission { get; set; }

    public string? Note { get; set; }

    public Guid RecordedByUserId { get; set; }

    public PaymentStatus Status { get; set; }

    public DateTimeOffset? RefundedAt { get; set; }

    public string? RefundNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public uint Version { get; set; }
}

/// <summary>A discount code (spec §5.1), often a teacher's: the teacher earns a commission on payments with it (spec §9).</summary>
public sealed class PromoCode
{
    public Guid Id { get; set; }

    /// <summary>Stored in upper case; matched without regard to case.</summary>
    public string Code { get; set; } = "";

    public Guid? TeacherUserId { get; set; }

    public int DiscountPercent { get; set; }

    public int CommissionPercent { get; set; } = 30;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? ValidUntil { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();
}

public static class PremiumMath
{
    /// <summary>Price after a percentage discount, rounded to a whole won or so‘m.</summary>
    public static int Discounted(int price, int discountPercent) =>
        (int)Math.Round(price * (100 - Math.Clamp(discountPercent, 0, 100)) / 100.0, MidpointRounding.AwayFromZero);

    /// <summary>The teacher's commission on the amount actually paid.</summary>
    public static int Commission(int amount, int commissionPercent) =>
        (int)Math.Round(amount * Math.Clamp(commissionPercent, 0, 100) / 100.0, MidpointRounding.AwayFromZero);

    /// <summary>A new period starts at the end of the latest one still running, or now (spec §7.3).</summary>
    public static DateTimeOffset NextStart(DateTimeOffset now, IEnumerable<DateTimeOffset> currentEnds) =>
        currentEnds.Where(e => e > now).DefaultIfEmpty(now).Max();
}

/// <summary>Amounts as people write them (L-48): "₩5,000" and "29 000 so‘m".</summary>
public static class Money
{
    public static string Format(int amount, Currency currency) => currency == Currency.Uzs
        ? $"{amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ')} so‘m"
        : $"₩{amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}";
}

/// <summary>
/// L-64: the Premium page opened for a locked feature (<c>premium?f=xatolar</c>), counted per feature and Korea day,
/// so the admin sees which feature brings learners to Premium. Nothing about the learner is stored.
/// </summary>
public sealed class PremiumView
{
    public string Feature { get; set; } = "";

    public DateOnly Day { get; set; }

    public int Count { get; set; }
}
