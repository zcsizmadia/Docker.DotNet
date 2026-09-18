namespace Microsoft.Net.Http.Client;

internal sealed class BufferedReadStream : WriteClosableStream, IPeekableStream
{
    private readonly Stream _inner;

    private readonly Socket? _socket;

    private readonly byte[] _buffer;

    private readonly ILogger _logger;

    private int _bufferRefCount;

    private int _bufferOffset;

    private int _bufferCount;

    private MemoryStream? _readLineBuffer;

    public BufferedReadStream(Stream inner, Socket? socket, ILogger logger)
        : this(inner, socket, 8192, logger)
    {
    }

    public BufferedReadStream(Stream inner, Socket? socket, int bufferLength, ILogger logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _socket = socket;
        _buffer = ArrayPool<byte>.Shared.Rent(bufferLength);
        _logger = logger;
        _bufferRefCount = 1;
    }

    public override bool CanRead
        => _inner.CanRead || _bufferCount > 0;

    public override bool CanSeek
        => false;

    public override bool CanWrite
        => _inner.CanWrite;

    public override bool CanTimeout
        => _inner.CanTimeout;

    public override bool CanCloseWrite
        => _socket != null || _inner is WriteClosableStream;

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReturnBuffer();

            _readLineBuffer?.Dispose();

            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override async ValueTask DisposeAsync()
    {
        ReturnBuffer();

        _readLineBuffer?.Dispose();

        await _inner.DisposeAsync()
            .ConfigureAwait(false);

        GC.SuppressFinalize(this);
    }
#endif

    public override void Flush()
        => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken)
        => _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = ReadBuffer(buffer.AsSpan(offset, count));
        if (read > 0)
        {
            return read;
        }

        return _inner.Read(buffer, offset, count);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int read = ReadBuffer(buffer.AsSpan(offset, count));
        if (read > 0)
        {
            return Task.FromResult(read);
        }

        return _inner.ReadAsync(buffer, offset, count, cancellationToken);
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override int Read(Span<byte> buffer)
    {
        int read = ReadBuffer(buffer);
        if (read > 0)
        {
            return read;
        }

        return _inner.Read(buffer);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = ReadBuffer(buffer.Span);
        if (read > 0)
        {
            return new ValueTask<int>(read);
        }

        return _inner.ReadAsync(buffer, cancellationToken);
    }
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

    public override void CloseWrite()
    {
        if (_socket != null)
        {
            _socket.Shutdown(SocketShutdown.Send);
            return;
        }

        if (_inner is WriteClosableStream writeClosableStream)
        {
            writeClosableStream.CloseWrite();
            return;
        }

        throw new NotSupportedException("Cannot shutdown write on this transport");
    }

    public bool Peek(byte[] buffer, uint toPeek, out uint peeked, out uint available, out uint remaining)
    {
        int read = PeekBuffer(buffer, toPeek, out peeked, out available, out remaining);
        if (read > 0)
        {
            return true;
        }

        if (_inner is IPeekableStream peekableStream)
        {
            return peekableStream.Peek(buffer, toPeek, out peeked, out available, out remaining);
        }

        throw new NotSupportedException("_inner stream isn't a peekable stream");
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (_readLineBuffer == null)
        {
            _readLineBuffer = new MemoryStream();
        }
        else
        {
            _readLineBuffer.SetLength(0);
        }

        const byte cr = (byte)'\r';
        const byte lf = (byte)'\n';

        bool crFound = false;

        while (true)
        {
            if (_bufferCount == 0)
            {
                _bufferOffset = 0;

                _bufferCount = await _inner.ReadAsync(_buffer.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);

                if (_bufferCount == 0)
                {
                    if (crFound)
                    {
                        _readLineBuffer.WriteByte(cr);
                    }

                    if (_readLineBuffer.Length == 0)
                    {
                        return null;
                    }

                    break; // return what is left in _readLineBuffer
                }
            }

            if (crFound)
            {
                if (_buffer[_bufferOffset] == lf)
                {
                    _bufferOffset++;
                    _bufferCount--;
                    break;
                }
                crFound = false;
                _readLineBuffer.WriteByte(cr);
            }

            var crIndex = _buffer.AsSpan(_bufferOffset, _bufferCount).IndexOf(cr);
            if (crIndex != -1)
            {
                _readLineBuffer.Write(_buffer, _bufferOffset, crIndex);
                _bufferOffset += crIndex + 1;
                _bufferCount -= crIndex + 1;

                if (_bufferCount > 0)
                {
                    if (_buffer[_bufferOffset] == lf)
                    {
                        _bufferOffset++;
                        _bufferCount--;
                        break;
                    }
                    _readLineBuffer.WriteByte(cr);
                }
                else
                {
                    crFound = true;
                }
            }
            else
            {
                _readLineBuffer.Write(_buffer, _bufferOffset, _bufferCount);
                _bufferCount = 0;
            }
        }

        return Encoding.ASCII.GetString(_readLineBuffer.GetBuffer(), 0, (int)_readLineBuffer.Length);
    }

    private int ReadBuffer(Span<byte> destination)
    {
        if (_bufferCount > 0)
        {
            int toCopy = Math.Min(_bufferCount, destination.Length);
            _buffer.AsSpan(_bufferOffset, toCopy).CopyTo(destination);
            _bufferOffset += toCopy;
            _bufferCount -= toCopy;
            return toCopy;
        }

        return 0;
    }

    private int PeekBuffer(byte[] buffer, uint toPeek, out uint peeked, out uint available, out uint remaining)
    {
        if (_bufferCount > 0)
        {
            int toCopy = Math.Min(_bufferCount, (int)toPeek);
            _buffer.AsSpan(_bufferOffset, toCopy).CopyTo(buffer);
            peeked = (uint)toCopy;
            available = (uint)_bufferCount;
            remaining = available - peeked;
            return toCopy;
        }

        peeked = 0;
        available = 0;
        remaining = 0;
        return 0;
    }

    private void ReturnBuffer()
    {
        if (Interlocked.Exchange(ref _bufferRefCount, 0) == 1)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }
    }
}