namespace Docker.DotNet.Tests;

/// <summary>
/// A read-only stream that hands out at most a fixed number of bytes per read. It is used to
/// exercise the code paths where a payload is split across several reads, which is what the
/// stream implementations see when reading from a socket or a named pipe.
/// </summary>
internal sealed class PartialReadStream : Stream
{
    private readonly byte[] _data;

    private readonly int _maxBytesPerRead;

    private int _position;

    public PartialReadStream(byte[] data, int maxBytesPerRead)
    {
        _data = data;
        _maxBytesPerRead = maxBytesPerRead;
    }

    public PartialReadStream(string data, int maxBytesPerRead)
        : this(Encoding.ASCII.GetBytes(data), maxBytesPerRead)
    {
    }

    public bool IsDisposed { get; private set; }

    public bool IsDisposedAsynchronously { get; private set; }

    public override bool CanRead
        => true;

    public override bool CanSeek
        => false;

    public override bool CanWrite
        => false;

    public override long Length
        => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
        => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var count = Math.Min(Math.Min(buffer.Length, _maxBytesPerRead), _data.Length - _position);

        _data.AsSpan(_position, count).CopyTo(buffer);
        _position += count;

        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => Task.FromResult(Read(buffer.AsSpan(offset, count)));

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => new ValueTask<int>(Read(buffer.Span));

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        IsDisposedAsynchronously = true;
        IsDisposed = true;
        return default;
    }
}

/// <summary>
/// A write-only stream that records everything written to it, so that the exact bytes a stream
/// implementation puts on the wire can be asserted on.
/// </summary>
internal sealed class RecordingStream : Stream
{
    private readonly MemoryStream _written = new MemoryStream();

    public bool IsDisposed { get; private set; }

    public bool IsDisposedAsynchronously { get; private set; }

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

    public byte[] ToArray()
        => _written.ToArray();

    public string ToAsciiString()
        => Encoding.ASCII.GetString(_written.GetBuffer(), 0, (int)_written.Length);

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => _written.Write(buffer, offset, count);

    public override void Write(ReadOnlySpan<byte> buffer)
        => _written.Write(buffer);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        _written.Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _written.Write(buffer.Span);
        return default;
    }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        IsDisposedAsynchronously = true;
        IsDisposed = true;
        return default;
    }
}
/// <summary>
/// A stream that supports timeouts, used to verify that the wrapper streams forward the timeout
/// properties rather than reporting support and then throwing.
/// </summary>
internal sealed class TimeoutCapableStream : Stream
{
    private int _readTimeout = 1000;

    private int _writeTimeout = 2000;

    public override bool CanRead
        => true;

    public override bool CanSeek
        => false;

    public override bool CanWrite
        => true;

    public override bool CanTimeout
        => true;

    public override int ReadTimeout
    {
        get => _readTimeout;
        set => _readTimeout = value;
    }

    public override int WriteTimeout
    {
        get => _writeTimeout;
        set => _writeTimeout = value;
    }

    public override long Length
        => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
        => 0;

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
    }
}