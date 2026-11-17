using System.Net;
using System.Net.Sockets;
using Lugat.Application.Reminders;

namespace Lugat.Infrastructure.Push;

/// <summary>Pin each connection to a public address of an approved service. Never follow redirects.</summary>
public static class PushTransport
{
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        ConnectCallback = async (context, ct) =>
        {
            if (!PushEndpointPolicy.IsAllowedHost(context.DnsEndPoint.Host) || context.DnsEndPoint.Port != 443)
            {
                throw new HttpRequestException("Push service is not allowed.");
            }

            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            if (addresses.Length == 0 || addresses.Any(a => !IsPublicAddress(a)))
            {
                throw new HttpRequestException("Push service address is not public.");
            }

            foreach (var address in addresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, 443), ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (SocketException)
                {
                    socket.Dispose();
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }

            throw new HttpRequestException("Push service connection failed.");
        },
    };

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // Accept global unicast only; reject local, multicast, mapped/tunnel and documentation space.
            return (b[0] & 0xe0) == 0x20 && !(b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8)
                && !(b[0] == 0x20 && b[1] == 0x02) && !(b[0] == 0x20 && b[1] == 0x01 && b[2] == 0 && b[3] == 0);
        }

        return address.AddressFamily == AddressFamily.InterNetwork
            && b[0] is not (0 or 10 or 127) && b[0] < 224
            && !(b[0] == 100 && b[1] is >= 64 and <= 127)
            && !(b[0] == 169 && b[1] == 254) && !(b[0] == 172 && b[1] is >= 16 and <= 31)
            && !(b[0] == 192 && (b[1] == 168 || b[1] == 0 || b[1] == 2))
            && !(b[0] == 198 && (b[1] is 18 or 19 || b[1] == 51 && b[2] == 100))
            && !(b[0] == 203 && b[1] == 0 && b[2] == 113);
    }
}
