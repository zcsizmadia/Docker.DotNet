namespace Microsoft.Net.Http.Client;

internal sealed class ChunkedWriteStream : Stream
{
    // A zero length chunk, followed by an empty trailer section, terminates the chunked body.
    private static readonly ReadOnlyMemory<byte> EndOfContentBytes = "0\r\n\r\n"u8.ToArray();

    private static readonly ReadOnlyMemory<byte> ChunkFooterBytes = "\r\n"u8.ToArray();

    // The longest hexadecimal representation of a positive 32-bit chunk size, followed by CRLF.
    private const int MaxChunkHeaderLength = 8 + 2;

    private readonly Stream _inner;

    // Writes are sequential, so a single reusable header buffer per stream is sufficient.
    private readonly byte[] _chunkHeader = new byte[MaxChunkHeaderLength];

    public ChunkedWriteStream(Stream stream)
    {
        _inner = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public override bool CanRead
        => false;

    public override bool CanSeek
        => false;

    public override bool CanWrite
        => true;

    public override long Length
        => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
        => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken)
        => _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => WriteAsyncCore(buffer.AsMemory(offset, count), cancellationToken).AsTask();

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override int Read(Span<byte> buffer)
        => throw new NotSupportedException();

    public override void Write(ReadOnlySpan<byte> buffer)
        => throw new NotSupportedException();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => WriteAsyncCore(buffer, cancellationToken);
#endif

    public Task EndContentAsync(CancellationToken cancellationToken)
        => _inner.WriteAsync(EndOfContentBytes, cancellationToken).AsTask();

    private async ValueTask WriteAsyncCore(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (buffer.IsEmpty)
        {
            return;
        }

        var chunkHeaderLength = FormatChunkHeader(buffer.Length);

        // Write the chunk header (the chunk size in hexadecimal, followed by CRLF)
        await _inner.WriteAsync(_chunkHeader.AsMemory(0, chunkHeaderLength), cancellationToken)
            .ConfigureAwait(false);

        // Write the chunk data
        await _inner.WriteAsync(buffer, cancellationToken)
            .ConfigureAwait(false);

        // Write the chunk footer (CRLF)
        await _inner.WriteAsync(ChunkFooterBytes, cancellationToken)
            .ConfigureAwait(false);
    }

    private int FormatChunkHeader(int chunkLength)
    {
        if (!Utf8Formatter.TryFormat(chunkLength, _chunkHeader, out var bytesWritten, new StandardFormat('X')))
        {
            throw new InvalidOperationException($"Unable to format a chunk header for a chunk of {chunkLength} bytes.");
        }

        _chunkHeader[bytesWritten++] = (byte)'\r';
        _chunkHeader[bytesWritten++] = (byte)'\n';

        return bytesWritten;
    }
}