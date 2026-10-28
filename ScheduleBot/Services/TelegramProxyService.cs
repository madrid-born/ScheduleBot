using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ScheduleBot.Services;

public static class TelegramProxyService
{
    public static async Task<TcpClient> ConnectAsync(Uri proxy, string address, int port, CancellationToken ct = default)
    {
        if (proxy.Scheme != "socks5" || proxy.Port <= 0)
            throw new InvalidOperationException("MTProto proxy must use a socks5:// address with a port.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var connection = new TcpClient { NoDelay = true };
        try
        {
            await connection.ConnectAsync(proxy.DnsSafeHost, proxy.Port, deadline.Token);
            var stream = connection.GetStream();
            var credentials = proxy.UserInfo.Split(':', 2);
            var authenticated = !string.IsNullOrEmpty(proxy.UserInfo);
            await stream.WriteAsync(new byte[] { 5, 1, authenticated ? (byte)2 : (byte)0 }, deadline.Token);
            var reply = new byte[2];
            await stream.ReadExactlyAsync(reply, deadline.Token);
            if (reply[0] != 5 || reply[1] != (authenticated ? 2 : 0))
                throw new IOException("SOCKS5 proxy rejected the authentication method.");
            if (authenticated)
            {
                var username = Encoding.UTF8.GetBytes(Uri.UnescapeDataString(credentials[0]));
                var password = Encoding.UTF8.GetBytes(Uri.UnescapeDataString(credentials.ElementAtOrDefault(1) ?? ""));
                if (username.Length is 0 or > 255 || password.Length is 0 or > 255)
                    throw new InvalidOperationException("SOCKS5 credentials have an invalid length.");
                var auth = new byte[3 + username.Length + password.Length];
                auth[0] = 1; auth[1] = (byte)username.Length;
                username.CopyTo(auth, 2);
                auth[2 + username.Length] = (byte)password.Length;
                password.CopyTo(auth, 3 + username.Length);
                await stream.WriteAsync(auth, deadline.Token);
                await stream.ReadExactlyAsync(reply, deadline.Token);
                if (reply[0] != 1 || reply[1] != 0) throw new IOException("SOCKS5 authentication failed.");
            }

            var request = new List<byte> { 5, 1, 0 };
            if (IPAddress.TryParse(address.Trim('[', ']'), out var ip))
            {
                request.Add(ip.AddressFamily == AddressFamily.InterNetwork ? (byte)1 : (byte)4);
                request.AddRange(ip.GetAddressBytes());
            }
            else
            {
                var host = Encoding.ASCII.GetBytes(new IdnMapping().GetAscii(address));
                if (host.Length is 0 or > 255) throw new InvalidOperationException("SOCKS5 destination has an invalid length.");
                request.Add(3); request.Add((byte)host.Length); request.AddRange(host);
            }
            if (port is <= 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            request.Add((byte)(port >> 8)); request.Add((byte)port);
            await stream.WriteAsync(request.ToArray(), deadline.Token);
            var header = new byte[4];
            await stream.ReadExactlyAsync(header, deadline.Token);
            if (header[0] != 5 || header[1] != 0 || header[2] != 0)
                throw new IOException("SOCKS5 proxy could not connect to Telegram.");
            var addressSize = header[3] switch { 1 => 4, 4 => 16, 3 => -1, _ => throw new IOException("Invalid SOCKS5 address type.") };
            if (addressSize == -1)
            {
                await stream.ReadExactlyAsync(reply.AsMemory(0, 1), deadline.Token);
                addressSize = reply[0];
            }
            await stream.ReadExactlyAsync(new byte[addressSize + 2], deadline.Token);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
