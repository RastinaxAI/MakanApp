using System.Net;
using System.Net.Sockets;
using MakanApp.Application.Notification;

namespace MakanApp.Infrastructure.Notification;

public static class WebPushNetworkPolicy
{
    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var a = bytes[0]; var b = bytes[1]; var c = bytes[2];
            return a is not (0 or 10 or 127) && a < 224 &&
                !(a == 100 && b is >= 64 and <= 127) && !(a == 169 && b == 254) &&
                !(a == 172 && b is >= 16 and <= 31) && !(a == 192 && (b is 0 or 168 || b == 88 && c == 99)) &&
                !(a == 198 && (b is 18 or 19 || b == 51 && c == 100)) && !(a == 203 && b == 0 && c == 113);
        }
        return address.AddressFamily == AddressFamily.InterNetworkV6 && (bytes[0] & 0xE0) == 0x20 &&
            !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] < 2) &&
            !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8) &&
            !(bytes[0] == 0x20 && bytes[1] == 0x02) &&
            !(bytes[0] == 0x3F && bytes[1] == 0xFF && bytes[2] < 0x10);
    }

    public static SocketsHttpHandler CreateHandler(PushDeviceOptions options) => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 20,
        ConnectCallback = async (context, cancellationToken) =>
        {
            if (context.DnsEndPoint.Port != 443 ||
                !options.AllowedWebPushHosts.Contains(context.DnsEndPoint.Host, StringComparer.OrdinalIgnoreCase))
                throw new HttpRequestException("مقصد Push مجاز نیست.");
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
            if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
                throw new HttpRequestException("آدرس شبکه مقصد Push مجاز نیست.");
            // همان IP بررسی‌شده به socket داده می‌شود؛ resolve دوم و DNS rebinding رخ نمی‌دهد.
            foreach (var address in addresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, 443), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (SocketException) { socket.Dispose(); }
                catch { socket.Dispose(); throw; }
            }
            throw new HttpRequestException("اتصال به سرویس Push برقرار نشد.");
        }
    };
}

public sealed class WebPushResponseSanitizingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        // فقط status/header لازم‌اند؛ بدنهٔ ناشناخته وارد exception یا log کتابخانه نمی‌شود.
        response.Content.Dispose();
        response.Content = new ByteArrayContent([]);
        return response;
    }
}
