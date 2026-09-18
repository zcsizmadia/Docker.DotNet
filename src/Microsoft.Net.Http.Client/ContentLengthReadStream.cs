namespace Microsoft.Net.Http.Client;

internal sealed class ContentLengthReadStream : Stream
{
    private readonly Stream _inner;
    private long _bytesRemaining;
    private bool _disposed;

    public ContentLengthReadStream(Stream inner, long contentLength)
    {
        _inner = inner;
        _bytesRemaining = contentLength;
    }

    public override bool CanRead
        => !_disposed;

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
        get
        {
            CheckDisposed();
            return _inner.ReadTimeout;
        }
        set
        {
            CheckDisposed();
            _inner.ReadTimeout = value;
        }
    }

    public override int WriteTimeout
    {
        get
        {
            CheckDisposed();
            return _inner.WriteTimeout;
        }
        set
        {
            CheckDisposed();
            _inner.WriteTimeout = value;
        }
    }

    public override void Flush()
        => throw new NotSupportedException();

    private void UpdateBytesRemaining(int read)
    {
        _bytesRemaining -= read;
        if (_bytesRemaining <= 0)
        {
            _disposed = true;
        }
        System.Diagnostics.Debug.Assert(_bytesRemaining >= 0, "Negative bytes remaining? " + _bytesRemaining);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        // TODO: Validate buffer
        if (_disposed)
        {
            return 0;
        }

        if (_bytesRemaining == 0)
        {
            return 0;
        }

        int toRead = (int)Math.Min(count, _bytesRemaining);
        int read = _inner.Read(buffer, offset, toRead);
        UpdateBytesRemaining(read);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsyncCore(buffer.AsMemory(offset, count), cancellationToken).AsTask();

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override int Read(Span<byte> buffer)
    {
        if (_disposed)
        {
            return 0;
        }

        if (_bytesRemaining == 0)
        {
            return 0;
        }

        int toRead = (int)Math.Min(buffer.Length, _bytesRemaining);
        int read = _inner.Read(buffer.Slice(0, toRead));
        UpdateBytesRemaining(read);
        return read;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => ReadAsyncCore(buffer, cancellationToken);
#endif

    private async ValueTask<int> ReadAsyncCore(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        // TODO: Validate args
        if (_disposed)
        {
            return 0;
        }

        if (_bytesRemaining == 0)
        {
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();
        int toRead = (int)Math.Min(buffer.Length, _bytesRemaining);
        int read = await _inner.ReadAsync(buffer.Slice(0, toRead), cancellationToken)
            .ConfigureAwait(false);
        UpdateBytesRemaining(read);
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // TODO: Sync drain with timeout if small number of bytes remaining? This will let us re-use the connection.
            _inner.Dispose();
        }
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync()
            .ConfigureAwait(false);

        GC.SuppressFinalize(this);
    }
#endif

    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(typeof(ContentLengthReadStream).FullName);
        }
    }
}