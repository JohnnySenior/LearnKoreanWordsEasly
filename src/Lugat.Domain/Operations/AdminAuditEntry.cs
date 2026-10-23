namespace Lugat.Domain.Operations;

/// <summary>
/// What an admin changed by hand and why (spec §10: "obuna muddatini qo‘lda o‘zgartirish (sabab bilan)"): role
/// changes, Premium given or taken away, commission marked paid.
/// </summary>
public sealed class AdminAuditEntry
{
    public Guid Id { get; set; }

    public Guid AdminUserId { get; set; }

    /// <summary>E.g. "roles", "premium", "payout".</summary>
    public string Action { get; set; } = "";

    public Guid? TargetUserId { get; set; }

    /// <summary>What changed, in Uzbek, e.g. "+30 kun: o‘qituvchi uchun sinov".</summary>
    public string Detail { get; set; } = "";

    public DateTimeOffset At { get; set; }
}
