using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using ScheduleBot.Models;
using TL;

namespace ScheduleBot.Services;

// A bot-authorized MTProto session used only for file retrieval; existing Bot API polling stays intact.
public sealed class MtProtoTelegramFileService : ITelegramFileSource, IAsyncDisposable
{
    private readonly FileLinkOptions options;
    private readonly TelegramFileService hosted;
    private readonly IConfiguration config;
    private readonly IHostEnvironment environment;
    private readonly SemaphoreSlim initGate = new(1);
    private readonly SemaphoreSlim rpcGate;
    private WTelegram.Client? client;
    private Task? login;
    private readonly CancellationTokenSource stopped = new();

    public MtProtoTelegramFileService(IOptions<FileLinkOptions> options, TelegramFileService hosted,
        IConfiguration config, IHostEnvironment environment)
    {
        this.options = options.Value;
        this.hosted = hosted;
        this.config = config;
        this.environment = environment;
        rpcGate = new SemaphoreSlim(this.options.MaxConcurrentDownloads);
    }

    public async Task<HttpResponseMessage> OpenAsync(FileLink reference, RangeHeaderValue? range, CancellationToken ct)
    {
        if (!options.HasMtProtoCredentials) throw new InvalidOperationException("MTProto file access is not configured.");
        // Photos/legacy identifier formats retain the hosted path. All document-like files use MTProto.
        if (!TryGetDocumentId(reference.FileUniqueId, out var documentId))
        {
            if (reference.FileSize > FileLinkOptions.MaxFileBytes) throw new FileNotFoundException();
            return await hosted.OpenAsync(reference, range, ct);
        }
        await TelegramFileDiagnostics.RunAsync("BotAuthorization", async () => { await EnsureLoginAsync(ct); return true; });
        var messages = await TelegramFileDiagnostics.RunAsync("MessageLookup", () =>
            CallAsync(() => client!.Messages_GetMessages(new InputMessage[] { new InputMessageID { id = reference.MessageId } }), ct));
        var document = ResolveDocument(reference, messages.Messages, documentId, options.MaxDownloadBytes, client!.UserId);
        var location = document.ToFileLocation();
        var dc = await TelegramFileDiagnostics.RunAsync("DataCenterConnect", () => CallAsync(() => client!.GetClientForDC(-document.dc_id), ct));
        return CreateResponse(document.size, range, (offset, limit, cancellation) =>
            TelegramFileDiagnostics.RunAsync("ChunkRead", () => CallAsync(async () =>
            {
                var result = await dc.Upload_GetFile(location, offset, limit);
                return result is Upload_File bytes ? bytes.bytes : throw new IOException("Unsupported Telegram file response.");
            }, cancellation)), ct);
    }

    public static Document ResolveDocument(FileLink reference, IEnumerable<MessageBase> messages, long documentId, long maxBytes, long botId = 0)
    {
        var message = messages.OfType<TL.Message>().SingleOrDefault(m => m.id == reference.MessageId
            && m.peer_id is PeerUser peer && (peer.user_id == reference.ChatId
                || botId > 0 && peer.user_id == botId && m.from_id is PeerUser sender && sender.user_id == reference.ChatId));
        if (message?.media is not MessageMediaDocument { document: Document document } || document.id != documentId
            || document.size < 0 || document.size > maxBytes
            || reference.FileSize is { } expected && expected != document.size)
            throw new FileNotFoundException();
        return document;
    }

    private async Task EnsureLoginAsync(CancellationToken ct)
    {
        await initGate.WaitAsync(ct);
        try
        {
            if (client == null)
            {
                var proxy = !string.IsNullOrWhiteSpace(options.MtProtoProxyUrl) ? options.MtProtoProxyUrl
                    : environment.IsDevelopment() ? config["Proxy:Url"] : null;
                Uri? proxyUri = null;
                if (!string.IsNullOrWhiteSpace(proxy) && (!Uri.TryCreate(proxy, UriKind.Absolute, out proxyUri)
                    || proxyUri.Scheme != "socks5" || proxyUri.Port <= 0))
                    throw new InvalidOperationException("MTProto proxy must use a socks5:// address with a port.");
                var path = Path.GetFullPath(options.MtProtoSessionPath, environment.ContentRootPath);
                var directory = Path.GetDirectoryName(path)!;
                if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
                else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                // Library logs can contain sensitive protocol details. Application logs use error types only.
                WTelegram.Helpers.Log = (_, _) => { };
                client = await TelegramFileDiagnostics.RunAsync("SessionOpen", () => Task.FromResult(new WTelegram.Client(key => key switch
                {
                    "api_id" => options.MtProtoApiId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "api_hash" => options.MtProtoApiHash,
                    "session_pathname" => path,
                    "phone_number" or "verification_code" or "password" => throw new InvalidOperationException("Personal account login is disabled."),
                    _ => WTelegram.Client.DefaultConfig(key)
                }) { FloodRetryThreshold = 0, MaxAutoReconnects = 3 }));
                if (proxyUri != null)
                    client.TcpHandler = (address, port) => TelegramProxyService.ConnectAsync(proxyUri, address, port, stopped.Token);
            }
            if (login == null || login.IsFaulted || login.IsCanceled)
                login = LoginAsync();
        }
        finally { initGate.Release(); }
        await login.WaitAsync(ct);
    }

    private async Task LoginAsync()
    {
        var token = config[$"{(environment.IsDevelopment() ? "Development" : "Production")}:BotToken"]
            ?? throw new InvalidOperationException("Bot token is missing.");
        await client!.LoginBotIfNeeded(token);
        client.DisableUpdates();
    }

    private async Task<T> CallAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        await rpcGate.WaitAsync(ct);
        Task<T>? pending = null;
        try
        {
            pending = operation();
            return await pending.WaitAsync(ct);
        }
        finally
        {
            // The protocol library cannot cancel an individual RPC. Keep its permit until it ends,
            // even if the HTTP client disconnected, so repeated cancellations cannot grow RPCs unboundedly.
            if (pending is { IsCompleted: false })
                _ = pending.ContinueWith(t => { _ = t.Exception; rpcGate.Release(); }, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            else rpcGate.Release();
        }
    }

    public static HttpResponseMessage CreateResponse(long size, RangeHeaderValue? range,
        Func<long, int, CancellationToken, Task<byte[]>> readChunk, CancellationToken ct)
    {
        long start = 0, length = size;
        var partial = range is { Unit: "bytes", Ranges.Count: 1 };
        if (partial)
        {
            var item = range!.Ranges.Single();
            start = item.From ?? Math.Max(0, size - (item.To ?? 0));
            var end = item.From == null ? size - 1 : Math.Min(item.To ?? long.MaxValue, size - 1);
            if (start > end || start >= size)
            {
                var rejected = new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                rejected.Content.Headers.ContentRange = new ContentRangeHeaderValue(size);
                return rejected;
            }
            length = end - start + 1;
        }
        var response = new HttpResponseMessage(partial ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
        {
            Content = new StreamContent(new TelegramChunkStream(size, start, length, readChunk, ct))
        };
        response.Content.Headers.ContentLength = length;
        if (partial) response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, start + length - 1, size);
        return response;
    }

    // TDLib file_unique_id: URL-safe base64, zero-run decoding, int32 type=2 then int64 document ID.
    // Fail closed for unknown/legacy formats; never infer identity from a filename or message text.
    public static bool TryGetDocumentId(string uniqueId, out long id)
    {
        id = 0;
        if (uniqueId.Length is 0 or > 128) return false;
        try
        {
            var encoded = uniqueId.Replace('-', '+').Replace('_', '/');
            var bytes = Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '='));
            Span<byte> decoded = stackalloc byte[12];
            decoded.Clear();
            int written = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0)
                {
                    if (++i >= bytes.Length || bytes[i] == 0 || written + bytes[i] > decoded.Length) return false;
                    written += bytes[i];
                }
                else
                {
                    if (written >= decoded.Length) return false;
                    decoded[written++] = bytes[i];
                }
            }
            if (written != 12 || BinaryPrimitives.ReadInt32LittleEndian(decoded) != 2) return false;
            id = BinaryPrimitives.ReadInt64LittleEndian(decoded[4..]);
            return id != 0;
        }
        catch (FormatException) { return false; }
    }

    public async ValueTask DisposeAsync()
    {
        stopped.Cancel();
        if (client != null) await client.DisposeAsync();
        // Pending RPC continuations release their permits after shutdown; do not dispose those semaphores early.
    }
}
