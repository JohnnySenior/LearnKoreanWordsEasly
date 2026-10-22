namespace Lugat.Domain.Reminders;

public enum DeliveryStatus
{
    /// <summary>Claimed for sending; if the process dies here the outcome is unknown and it is not resent.</summary>
    Sending,

    Sent,

    /// <summary>Nothing to practise, or the learner turned reminders off.</summary>
    Skipped,

    /// <summary>Telegram refused for a temporary reason; retried inside the delay window.</summary>
    RetryPending,

    /// <summary>The bot may not write to the user (not started or blocked). Shown in the app (spec NOT02).</summary>
    Blocked,

    /// <summary>Gave up; the reason is in <see cref="ReminderDelivery.Error"/>.</summary>
    Failed,

    /// <summary>Timed out or lost mid-request: the message may or may not have arrived (spec NOT03).</summary>
    Uncertain,
}

/// <summary>
/// One reminder occurrence in the notification outbox (spec §12 NotificationOutbox). The unique
/// <see cref="OccurrenceKey"/> makes sending at most once per occurrence, even across restarts (spec AC10).
/// </summary>
public sealed class ReminderDelivery
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string OccurrenceKey { get; set; } = "";

    public ReminderSlot Slot { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }

    public DeliveryStatus Status { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? NextRetryAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Telegram message id when sent.</summary>
    public long? ProviderMessageId { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Concurrency token, so only one worker can take over a retry.</summary>
    public uint Version { get; set; }
}
