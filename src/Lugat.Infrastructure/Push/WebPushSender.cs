using System.Net;
using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Lugat.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lugat.Infrastructure.Push;

/// <summary>VAPID keys (L-63): Push__PublicKey, Push__PrivateKey, Push__Subject. Without both keys push is off.</summary>
public sealed class PushOptions
{
    public const string SectionName = "Push";

    public string? PublicKey { get; set; }

    public string? PrivateKey { get; set; }

    /// <summary>Who runs the server, for the push services: a mailto: or https: address.</summary>
    public string Subject { get; set; } = "https://koreyscha.com";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);
}

/// <summary>Web Push (RFC 8030/8291/8292) through Lib.Net.Http.WebPush (L-63).</summary>
public sealed class WebPushSender(HttpClient http, IOptions<PushOptions> options, ILogger<WebPushSender> logger) : IWebPushSender
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private PushOptions Options => options.Value;

    public bool IsConfigured => Options.IsConfigured;

    public string? PublicKey => IsConfigured ? Options.PublicKey!.Trim() : null;

    public async Task<PushOutcome> SendAsync(PushTarget target, PushNotification notification, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return PushOutcome.Failed;
        }

        var client = new PushServiceClient(http)
        {
            DefaultAuthentication = new VapidAuthentication(Options.PublicKey!.Trim(), Options.PrivateKey!.Trim()) { Subject = Options.Subject.Trim() },
        };
        var subscription = new PushSubscription { Endpoint = target.Endpoint, Keys = new Dictionary<string, string> { ["p256dh"] = target.P256dh, ["auth"] = target.Auth } };
        // A reminder older than 30 minutes is worth nothing (spec NOT03): the push service drops it after that.
        var message = new PushMessage(JsonSerializer.Serialize(notification, Json)) { TimeToLive = 30 * 60, Urgency = PushMessageUrgency.Normal };
        try
        {
            await client.RequestPushMessageDeliveryAsync(subscription, message, ct);
            return PushOutcome.Sent;
        }
        catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushOutcome.Gone;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // The endpoint is a device address: only the push service's host goes to the log.
            logger.LogWarning("Web Push to {Host} failed: {Error}", Uri.TryCreate(target.Endpoint, UriKind.Absolute, out var u) ? u.Host : "?",
                ex is PushServiceClientException p ? $"{(int)p.StatusCode}" : ex.GetType().Name);
            return PushOutcome.Failed;
        }
    }
}
