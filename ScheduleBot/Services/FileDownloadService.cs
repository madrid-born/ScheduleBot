using System.Threading.RateLimiting;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Telegram.Bot.Exceptions;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

// Both limits are global to this process, with no waiting queue or per-token state growth.
public sealed class FileDownloadLimits(IOptions<FileLinkOptions> options) : IDisposable
{
    public ConcurrencyLimiter Concurrent { get; } = new(new ConcurrencyLimiterOptions
    { PermitLimit = options.Value.MaxConcurrentDownloads, QueueLimit = 0 });
    public TokenBucketRateLimiter Requests { get; } = new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = options.Value.RequestsPerMinute, TokensPerPeriod = options.Value.RequestsPerMinute,
        ReplenishmentPeriod = TimeSpan.FromMinutes(1), AutoReplenishment = true, QueueLimit = 0
    });
    public void Dispose() { Concurrent.Dispose(); Requests.Dispose(); }
}

public sealed class FileDownloadService(FileLinkService links, ITelegramFileSource source, FileDownloadLimits limits,
    IOptions<FileLinkOptions> options, TimeProvider clock, ILogger<FileDownloadService> logger)
{
    public async Task HandleAsync(HttpContext context, string token)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        using var rate = limits.Requests.AttemptAcquire();
        using var slot = limits.Concurrent.AttemptAcquire();
        if (!rate.IsAcquired || !slot.IsAcquired)
        {
            context.Response.StatusCode = 429;
            context.Response.Headers.RetryAfter = "60";
            return;
        }
        if (!FileLinkService.ValidToken(token)) { context.Response.StatusCode = 404; return; }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromMinutes(options.Value.DownloadTimeoutMinutes));
        var ct = timeout.Token;
        try
        {
            var file = await links.ResolveAsync(token, ct);
            if (file == null) { context.Response.StatusCode = 404; return; }
            if (file.Revoked || file.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
            { context.Response.StatusCode = 410; return; }
            RangeHeaderValue? range = null;
            // No stable HTTP validators are exposed, so If-Range requests receive the full representation.
            if (!context.Request.Headers.ContainsKey("If-Range") &&
                RangeHeaderValue.TryParse(context.Request.Headers.Range.ToString(), out var requested)
                && requested.Unit == "bytes" && requested.Ranges.Count == 1)
                range = requested;
            using var upstream = await source.OpenAsync(file, range, ct);
            var contentRange = upstream.Content.Headers.ContentRange;
            if (upstream.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable && range != null
                && contentRange is { Unit: "bytes", HasRange: false, Length: >= 0 }
                && contentRange.Length <= options.Value.EffectiveMaxFileBytes)
            {
                context.Response.StatusCode = 416;
                context.Response.Headers.ContentRange = contentRange.ToString();
                context.Response.ContentLength = 0;
                return;
            }
            if (!upstream.IsSuccessStatusCode)
            {
                context.Response.StatusCode = upstream.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden ? 404 : 502;
                return;
            }
            var partial = upstream.StatusCode == System.Net.HttpStatusCode.PartialContent;
            if ((partial ? !ValidPartial(range, contentRange, upstream.Content.Headers.ContentLength, options.Value.EffectiveMaxFileBytes)
                    : upstream.StatusCode != System.Net.HttpStatusCode.OK) ||
                upstream.Content.Headers.ContentLength > options.Value.EffectiveMaxFileBytes)
            { context.Response.StatusCode = 502; return; }
            context.Response.ContentType = FileLinkService.SafeMime(file.ContentType);
            var disposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
            disposition.SetHttpFileName(FileLinkService.SafeName(file.FileName));
            context.Response.Headers.ContentDisposition = disposition.ToString();
            // Advertise ranges only when the upstream actually honored this request.
            context.Response.Headers.AcceptRanges = partial ? "bytes" : "none";
            if (partial)
            {
                context.Response.StatusCode = 206;
                context.Response.Headers.ContentRange = contentRange!.ToString();
            }
            context.Response.ContentLength = upstream.Content.Headers.ContentLength;
            await using var stream = await upstream.Content.ReadAsStreamAsync(ct);
            await CopyAsync(stream, context.Response.Body, ct, options.Value.EffectiveMaxFileBytes);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { context.Abort(); }
        catch (TelegramFileTransferException ex)
        {
            logger.LogWarning("Telegram file operation failed at {Stage} ({Reason}).", ex.Stage, ex.Reason);
            if (context.Response.HasStarted) context.Abort();
            else
            {
                context.Response.Clear();
                context.Response.Headers.CacheControl = "no-store";
                context.Response.StatusCode = 502;
            }
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            logger.LogWarning("Telegram file connection failed ({SocketError}). Check Telegram connectivity and the configured proxy.", ex.SocketErrorCode);
            if (context.Response.HasStarted) context.Abort();
            else
            {
                context.Response.Clear();
                context.Response.Headers.CacheControl = "no-store";
                context.Response.StatusCode = 502;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or ApiRequestException or OperationCanceledException
            or TL.RpcException or WTelegram.WTException)
        {
            // Exception messages can include private upstream URLs. Never log them.
            logger.LogWarning("Telegram file stream failed ({ErrorType}).", ex.GetType().Name);
            if (context.Response.HasStarted) context.Abort();
            else
            {
                context.Response.Clear();
                context.Response.Headers.CacheControl = "no-store";
                context.Response.StatusCode = ex is FileNotFoundException || ex is ApiRequestException { ErrorCode: 400 or 403 or 404 } ? 404 :
                    ex is OperationCanceledException ? 504 : 502;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("File link service failed ({ErrorType}).", ex.GetType().Name);
            if (context.Response.HasStarted) context.Abort();
            else
            {
                context.Response.Clear();
                context.Response.Headers.CacheControl = "no-store";
                context.Response.StatusCode = 503;
            }
        }
    }

    private static bool ValidPartial(RangeHeaderValue? requested, ContentRangeHeaderValue? actual, long? length, long maxBytes)
    {
        if (requested == null || actual is not { Unit: "bytes", HasRange: true, Length: > 0 } || actual.Length > maxBytes
            || actual.From == null || actual.To == null || actual.To >= actual.Length) return false;
        var item = requested.Ranges.Single();
        var start = item.From ?? Math.Max(0, actual.Length.Value - (item.To ?? 0));
        var end = item.From == null ? actual.Length.Value - 1 : Math.Min(item.To ?? long.MaxValue, actual.Length.Value - 1);
        return start <= end && actual.From == start && actual.To == end && length == end - start + 1;
    }

    // Fixed buffer and awaited writes provide backpressure. Also cap unknown-length upstream bodies.
    public static async Task CopyAsync(Stream input, Stream output, CancellationToken ct, long maxBytes = FileLinkOptions.MaxFileBytes)
    {
        var buffer = new byte[64 * 1024];
        long total = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, ct)) != 0)
        {
            total += count;
            if (total > maxBytes) throw new IOException("File exceeds configured download limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), ct);
        }
    }
}
