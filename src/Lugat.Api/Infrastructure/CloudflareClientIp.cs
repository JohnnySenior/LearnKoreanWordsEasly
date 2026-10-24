using System.Net;

namespace Lugat.Api.Infrastructure;

/// <summary>
/// With koreyscha.com proxied by Cloudflare (L-39), Railway's proxy sees Cloudflare's edge, so the client address from
/// X-Forwarded-For (L-33) is a Cloudflare one that many learners share. Cloudflare puts the visitor in
/// CF-Connecting-IP; it is taken only when the request really came from a Cloudflare address, so a client calling the
/// Railway domain directly cannot pick its own IP to dodge the rate limits.
/// </summary>
public static class CloudflareClientIp
{
    public const string Header = "CF-Connecting-IP";

    /// <summary>
    /// Cloudflare's published ranges (cloudflare.com/ips), unchanged since 2021. <c>Cloudflare:Ranges</c> replaces them
    /// if Cloudflare ever changes the list; an outdated list only makes such requests count by edge address again.
    /// </summary>
    public static readonly string[] DefaultRanges =
    [
        "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22", "141.101.64.0/18", "108.162.192.0/18",
        "190.93.240.0/20", "188.114.96.0/20", "197.234.240.0/22", "198.41.128.0/17", "162.158.0.0/15", "104.16.0.0/13",
        "104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",
        "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32", "2405:8100::/32", "2a06:98c0::/29",
        "2c0f:f248::/32",
    ];

    public static IApplicationBuilder UseCloudflareClientIp(this IApplicationBuilder app, IConfiguration config)
    {
        var configured = config.GetSection("Cloudflare:Ranges").Get<string[]>();
        var ranges = (configured is { Length: > 0 } ? configured : DefaultRanges).Select(IPNetwork.Parse).ToArray();
        return app.Use((http, next) =>
        {
            if (http.Connection.RemoteIpAddress is { } edge
                && ranges.Any(r => r.Contains(edge.IsIPv4MappedToIPv6 ? edge.MapToIPv4() : edge))
                && IPAddress.TryParse(http.Request.Headers[Header].ToString().Trim(), out var visitor))
            {
                http.Connection.RemoteIpAddress = visitor;
            }

            return next(http);
        });
    }
}
