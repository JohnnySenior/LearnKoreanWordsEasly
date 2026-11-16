namespace Lugat.Application.Reminders;

/// <summary>Only browser push services may receive device notifications, never an arbitrary user URL.</summary>
public static class PushEndpointPolicy
{
    public static bool IsAllowed(string endpoint) =>
        endpoint.Length <= 1000 && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443
        && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0
        && IsAllowedHost(uri.IdnHost);

    public static bool IsAllowedHost(string host) => host.Equals("fcm.googleapis.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("updates.push.services.mozilla.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("web.push.apple.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".notify.windows.com", StringComparison.OrdinalIgnoreCase);
}
