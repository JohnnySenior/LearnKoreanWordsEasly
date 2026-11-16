using System.Net;
using Lugat.Application.Reminders;
using Lugat.Infrastructure.Push;

namespace Lugat.Application.Tests.Push;

public class PushEndpointPolicyTests
{
    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc", true)]
    [InlineData("https://web.push.apple.com/abc", true)]
    [InlineData("https://wns2.notify.windows.com/abc", true)]
    [InlineData("https://fcm.googleapis.com.attacker.example/abc", false)]
    [InlineData("https://attacker.notify.windows.com.example/abc", false)]
    [InlineData("https://localhost/abc", false)]
    [InlineData("https://127.0.0.1/abc", false)]
    [InlineData("https://[::1]/abc", false)]
    [InlineData("https://10.0.0.1/abc", false)]
    [InlineData("https://fcm.googleapis.com:8443/abc", false)]
    [InlineData("https://user@fcm.googleapis.com/abc", false)]
    [InlineData("https://fcm.googleapis.com/abc#fragment", false)]
    [InlineData("http://fcm.googleapis.com/abc", false)]
    public void Only_browser_push_services_on_https_are_allowed(string endpoint, bool expected) =>
        Assert.Equal(expected, PushEndpointPolicy.IsAllowed(endpoint));

    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("8.8.8.8", true)]
    [InlineData("2606:4700:4700::1111", true)]
    public void The_connection_cannot_use_a_private_or_reserved_address(string address, bool expected) =>
        Assert.Equal(expected, PushTransport.IsPublicAddress(IPAddress.Parse(address)));

    [Fact]
    public void Redirects_and_environment_proxies_are_disabled()
    {
        using var handler = PushTransport.CreateHandler();
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
    }
}
