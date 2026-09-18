namespace Docker.DotNet;

internal sealed class StandardStreamResponse : Stream
{
    private readonly HttpResponseMessage _response;

    private readonly Stream _stream;

    public StandardStreamResponse(HttpResponseMessage response, Stream stream)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public HttpHeaders Headers
        => _response.Headers;

    public override bool CanRead
        => _stream.CanRead;

    public override bool CanSeek
        => _stream.CanSeek;

    public override bool CanWrite
        => _stream.CanWrite;

    public override bool CanTimeout
        => _stream.CanTimeout;

    public override long Length
        => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public override int ReadTimeout
    {
        get => _stream.ReadTimeout;
        set => _stream.ReadTimeout = value;
    }

    public override int WriteTimeout
    {
        get => _stream.WriteTimeout;
        set => _stream.WriteTimeout = value;
    }

    public override void Flush()
        => _stream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
        => _stream.Read(buffer, offset, count);

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override int Read(Span<byte> buffer)
        => _stream.Read(buffer);
#endif

    public override long Seek(long offset, SeekOrigin origin)
        => _stream.Seek(offset, origin);

    public override void SetLength(long value)
        => _stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
        => _stream.Write(buffer, offset, count);

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override void Write(ReadOnlySpan<byte> buffer)
        => _stream.Write(buffer);
#endif

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _stream.ReadAsync(buffer, offset, count, cancellationToken);

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _stream.ReadAsync(buffer, cancellationToken);
#endif

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _stream.WriteAsync(buffer, offset, count, cancellationToken);

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _stream.WriteAsync(buffer, cancellationToken);
#endif

    public override Task FlushAsync(CancellationToken cancellationToken)
        => _stream.FlushAsync(cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
            _response.Dispose();
        }

        base.Dispose(disposing);
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    public override async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync()
            .ConfigureAwait(false);

        // HttpResponseMessage does not implement IAsyncDisposable.
        _response.Dispose();

        GC.SuppressFinalize(this);
    }
#endif
}