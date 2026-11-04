using System.Net;
using System.Security.Cryptography;
using Lugat.Application.Abstractions;
using Lugat.Infrastructure.Push;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lugat.Application.Tests.Push;

/// <summary>L-63: the real sender against a fake push service — VAPID, encryption, TTL, and 410 meaning "gone".</summary>
public class WebPushSenderTests
{
    private sealed class PushService(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public byte[] Body { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            (Request, Body) = (request, await request.Content!.ReadAsByteArrayAsync(ct));
            return new HttpResponseMessage(status);
        }
    }

    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static (string Public, string Private) Vapid()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(true);
        return (B64([4, .. p.Q.X!, .. p.Q.Y!]), B64(p.D!));
    }

    /// <summary>A browser's subscription keys: its ECDH public key and a 16-byte auth secret.</summary>
    private static PushTarget Device()
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false);
        return new PushTarget("https://push.example/send/abc", B64([4, .. p.Q.X!, .. p.Q.Y!]), B64(RandomNumberGenerator.GetBytes(16)));
    }

    private static WebPushSender Sender(PushService service, string? publicKey, string? privateKey) =>
        new(new HttpClient(service), Options.Create(new PushOptions { PublicKey = publicKey, PrivateKey = privateKey }), NullLogger<WebPushSender>.Instance);

    private static readonly PushNotification Note = new("Koreyscha", "Bugun 3 ta so‘zni takrorlash vaqti. ~1 daqiqa.", "/takrorlash", "lugat-reminder");

    [Fact]
    public async Task A_reminder_goes_encrypted_with_vapid_and_a_30_minute_ttl()
    {
        var (pub, priv) = Vapid();
        var service = new PushService(HttpStatusCode.Created);
        var sender = Sender(service, pub, priv);

        Assert.True(sender.IsConfigured);
        Assert.Equal(pub, sender.PublicKey);
        Assert.Equal(PushOutcome.Sent, await sender.SendAsync(Device(), Note, CancellationToken.None));

        var request = service.Request!;
        Assert.Equal("https://push.example/send/abc", request.RequestUri!.ToString());
        Assert.Equal("vapid", request.Headers.Authorization!.Scheme);
        Assert.Contains("k=" + pub, request.Headers.Authorization.Parameter);
        Assert.Equal("1800", request.Headers.GetValues("TTL").Single());
        Assert.Equal("aes128gcm", request.Content!.Headers.ContentEncoding.Single());
        Assert.DoesNotContain("takrorlash", System.Text.Encoding.UTF8.GetString(service.Body)); // the text is encrypted
    }

    [Theory]
    [InlineData(HttpStatusCode.Gone, PushOutcome.Gone)]
    [InlineData(HttpStatusCode.NotFound, PushOutcome.Gone)]
    [InlineData(HttpStatusCode.TooManyRequests, PushOutcome.Failed)]
    [InlineData(HttpStatusCode.InternalServerError, PushOutcome.Failed)]
    public async Task A_gone_subscription_is_told_apart_from_a_passing_failure(HttpStatusCode status, PushOutcome expected)
    {
        var (pub, priv) = Vapid();
        Assert.Equal(expected, await Sender(new PushService(status), pub, priv).SendAsync(Device(), Note, CancellationToken.None));
    }

    [Fact]
    public async Task Without_keys_push_is_off_and_nothing_is_sent()
    {
        var service = new PushService(HttpStatusCode.Created);
        var sender = Sender(service, null, null);

        Assert.False(sender.IsConfigured);
        Assert.Null(sender.PublicKey);
        Assert.Equal(PushOutcome.Failed, await sender.SendAsync(Device(), Note, CancellationToken.None));
        Assert.Null(service.Request);
    }
}
