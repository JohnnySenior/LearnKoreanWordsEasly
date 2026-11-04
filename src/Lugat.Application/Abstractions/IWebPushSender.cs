namespace Lugat.Application.Abstractions;

public enum PushOutcome
{
    Sent,

    /// <summary>The push service answered 404 or 410: the subscription no longer exists and is deleted (L-63).</summary>
    Gone,

    /// <summary>Anything else (network, 429, 5xx); the subscription is kept.</summary>
    Failed,
}

/// <summary>The device to write to, as the browser gave it (PushSubscription.toJSON()).</summary>
public sealed record PushTarget(string Endpoint, string P256dh, string Auth);

/// <summary>
/// The notification the service worker shows (wwwroot/service-worker*.js). <paramref name="Url"/> is opened when it
/// is tapped; <paramref name="Tag"/> makes a newer reminder replace an older one on the same device.
/// </summary>
public sealed record PushNotification(string Title, string Body, string Url, string Tag);

/// <summary>Web Push with VAPID keys (L-63). Not configured without keys: push is then off and the app says so.</summary>
public interface IWebPushSender
{
    bool IsConfigured { get; }

    /// <summary>The VAPID public key the browser subscribes with; null when not configured.</summary>
    string? PublicKey { get; }

    Task<PushOutcome> SendAsync(PushTarget target, PushNotification notification, CancellationToken ct);
}
