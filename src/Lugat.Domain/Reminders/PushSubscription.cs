namespace Lugat.Domain.Reminders;

/// <summary>
/// One device's Web Push subscription (L-63): the browser's push service endpoint and the keys the message is
/// encrypted with. A learner may have several devices; the endpoint is unique, so subscribing again updates the row.
/// </summary>
public sealed class PushSubscription
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Endpoint { get; set; } = "";

    public string P256dh { get; set; } = "";

    public string Auth { get; set; } = "";

    /// <summary>Shown nowhere yet; kept so "which device" can be answered later.</summary>
    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastSuccessAt { get; set; }

    /// <summary>The last failed send that was not "gone"; a gone subscription is deleted instead.</summary>
    public DateTimeOffset? FailedAt { get; set; }
}
