namespace Microsoft.Net.Http.Client;

internal sealed class ChunkedReadStream : Stream
{
    private readonly BufferedReadStream _inner;

    private int _chunkBytesRemaining;

    private bool _done;

    public ChunkedReadStream(BufferedReadStream stream)
    {
        _inner = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public override bool CanRead
        => _inner.CanRead;

    public override bool CanSeek
        => false;

    public override bool CanWrite
        => false;

    public override bool CanTimeout
        => _inner.CanTimeout;

    public override long Length
        => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int ReadTimeout
    {
        get => _inner.ReadTimeout;
        set => _inner.ReadTimeout = value;
    }

    public override int WriteTimeout
    {
        get => _inner.WriteTimeout;
        set => _inner.WriteTimeout = value;
    }

    public override void Flush()
        => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsyncCore(buffer.AsMemory(offset, count), cancellationToken).AsTask();

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override int Read(Span<byte> buffer)
        => throw new NotSupportedException();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => ReadAsyncCore(buffer, cancellationToken);
#endif

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => _inner.Write(buffer, offset, count);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.WriteAsync(buffer, offset, count, cancellationToken);

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override void Write(ReadOnlySpan<byte> buffer)
        => _inner.Write(buffer);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.WriteAsync(buffer, cancellationToken);
#endif

    private async ValueTask<int> ReadAsyncCore(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (_done)
        {
            return 0;
        }

        if (_chunkBytesRemaining == 0)
        {
            var headerLine = await _inner.ReadLineAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!int.TryParse(headerLine, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _chunkBytesRemaining))
            {
                throw new IOException($"Invalid chunk header encountered: '{headerLine}'.");
            }
        }

        var readBytesCount = 0;

        if (_chunkBytesRemaining > 0)
        {
            var remainingBytesCount = Math.Min(_chunkBytesRemaining, buffer.Length);

            readBytesCount = await _inner.ReadAsync(buffer.Slice(0, remainingBytesCount), cancellationToken)
                .ConfigureAwait(false);

            if (readBytesCount == 0)
            {
                throw new EndOfStreamException();
            }

            _chunkBytesRemaining -= readBytesCount;
        }

        if (_chunkBytesRemaining == 0)
        {
            var emptyLine = await _inner.ReadLineAsync(cancellationToken)
                .ConfigureAwait(false);

            if (emptyLine == null)
            {
                throw new EndOfStreamException();
            }

            if (emptyLine.Length > 0)
            {
                throw new IOException($"Expected an empty line, but received: '{emptyLine}'.");
            }

            _done = readBytesCount == 0;
        }

        return readBytesCount;
    }
}