namespace Docker.DotNet.NativeHttp;

internal sealed class WriteClosableStreamWrapper(Stream stream) : WriteClosableStream
{
    private readonly Stream _stream = stream ?? throw new ArgumentNullException(nameof(stream));

    public override bool CanRead
        => _stream.CanRead;

    public override bool CanSeek
        => _stream.CanSeek;

    public override bool CanWrite
        => _stream.CanWrite;

    public override bool CanTimeout
        => _stream.CanTimeout;

    public override bool CanCloseWrite
        => true;

    public override long Length
        => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public override void Flush()
        => _stream.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken)
        => _stream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
        => _stream.Read(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _stream.ReadAsync(buffer, offset, count, cancellationToken);

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override int Read(Span<byte> buffer)
        => _stream.Read(buffer);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _stream.ReadAsync(buffer, cancellationToken);
#endif

    public override long Seek(long offset, SeekOrigin origin)
        => _stream.Seek(offset, origin);

    public override void SetLength(long value)
        => _stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
        => _stream.Write(buffer, offset, count);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _stream.WriteAsync(buffer, offset, count, cancellationToken);

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override void Write(ReadOnlySpan<byte> buffer)
        => _stream.Write(buffer);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _stream.WriteAsync(buffer, cancellationToken);
#endif

    public override void CloseWrite()
        => _stream.Close();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }

        base.Dispose(disposing);
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync()
            .ConfigureAwait(false);

        GC.SuppressFinalize(this);
    }
#endif
}