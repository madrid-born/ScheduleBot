using System.Net;
using System.Net.Http.Headers;
using Telegram.Bot;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public interface ITelegramFileSource
{
    Task<HttpResponseMessage> OpenAsync(FileLink reference, RangeHeaderValue? range, CancellationToken ct);
}

// Dedicated client: HttpClientFactory's URI logging would expose Telegram's bot token.
public sealed class TelegramFileService : ITelegramFileSource, IDisposable
{
    private readonly HttpClient client;
    private readonly ITelegramBotClient bot;
    private readonly string token;

    public TelegramFileService(ITelegramBotClient bot, IConfiguration config, IHostEnvironment environment)
    {
        this.bot = bot;
        token = config[$"{(environment.IsDevelopment() ? "Development" : "Production")}:BotToken"]!;
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(15), PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        var proxy = config["Proxy:Url"];
        if (environment.IsDevelopment() && !string.IsNullOrWhiteSpace(proxy)) handler.Proxy = new WebProxy(proxy);
        client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<HttpResponseMessage> OpenAsync(FileLink reference, RangeHeaderValue? range, CancellationToken ct)
    {
        var file = await bot.GetFile(reference.FileId, ct);
        if (file.FileSize > FileLinkOptions.MaxFileBytes || string.IsNullOrEmpty(file.FilePath))
            throw new FileNotFoundException();
        // Do not accept absolute/local paths or allow metadata to change the upstream host.
        var segments = file.FilePath.Split('/');
        if (segments.Any(s => s is "" or "." or ".." || s.Contains('\\') || s.Contains(':')))
            throw new FileNotFoundException();
        var path = string.Join('/', segments.Select(Uri.EscapeDataString));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.telegram.org/file/bot{token}/{path}");
        request.Headers.Range = range;
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    public void Dispose() => client.Dispose();
}
