namespace ScheduleBot.Services;

// Pull one aligned Telegram chunk at a time. Memory use is independent of file length.
public sealed class TelegramChunkStream : Stream
{
    public const int ChunkSize = 256 * 1024;
    private readonly Func<long, int, CancellationToken, Task<byte[]>> readChunk;
    private readonly long fileSize, end;
    private readonly CancellationTokenSource stopped;
    private byte[] buffer = Array.Empty<byte>();
    private long position, bufferOffset = -1;
    private bool disposed;

    public TelegramChunkStream(long fileSize, long start, long length,
        Func<long, int, CancellationToken, Task<byte[]>> readChunk, CancellationToken ct)
    {
        if (fileSize < 0 || start < 0 || length < 0 || start > fileSize || length > fileSize - start)
            throw new ArgumentOutOfRangeException(nameof(length));
        this.fileSize = fileSize;
        position = start;
        end = start + length;
        this.readChunk = readChunk;
        stopped = CancellationTokenSource.CreateLinkedTokenSource(ct);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, stopped.Token);
        linked.Token.ThrowIfCancellationRequested();
        if (position >= end || destination.Length == 0) return 0;
        var aligned = position / ChunkSize * ChunkSize;
        if (bufferOffset != aligned)
        {
            buffer = await readChunk(aligned, ChunkSize, linked.Token);
            if (buffer.Length != Math.Min(ChunkSize, fileSize - aligned))
                throw new EndOfStreamException("Unexpected Telegram chunk length.");
            bufferOffset = aligned;
        }
        var offset = (int)(position - aligned);
        var count = (int)Math.Min(Math.Min(destination.Length, buffer.Length - offset), end - position);
        buffer.AsMemory(offset, count).CopyTo(destination);
        position += count;
        return count;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            stopped.Cancel();
            stopped.Dispose();
            buffer = Array.Empty<byte>();
        }
        base.Dispose(disposing);
    }
    public override Task<int> ReadAsync(byte[] destination, int offset, int count, CancellationToken ct) => ReadAsync(destination.AsMemory(offset, count), ct).AsTask();
    public override int Read(byte[] destination, int offset, int count) => throw new NotSupportedException();
    public override bool CanRead => !disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] destination, int offset, int count) => throw new NotSupportedException();
}
