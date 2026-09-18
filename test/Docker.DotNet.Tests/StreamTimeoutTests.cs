namespace Docker.DotNet.Tests;

/// <summary>
/// A stream that reports <see cref="Stream.CanTimeout"/> as true must also implement
/// <see cref="Stream.ReadTimeout"/> and <see cref="Stream.WriteTimeout"/>. The base class
/// implementation of those properties throws, so a stream that overrides only CanTimeout breaks
/// the usual "if (stream.CanTimeout) stream.ReadTimeout = ..." guard.
/// </summary>
public sealed class StreamTimeoutTests
{
    [Fact]
    public void BufferedReadStream_ForwardsTheTimeouts_OfTheInnerStream()
    {
        using var inner = new TimeoutCapableStream();
        using var stream = new BufferedReadStream(inner, null, 8192, NullLogger.Instance);

        Assert.True(stream.CanTimeout);
        Assert.Equal(1000, stream.ReadTimeout);
        Assert.Equal(2000, stream.WriteTimeout);

        stream.ReadTimeout = 3000;
        stream.WriteTimeout = 4000;

        Assert.Equal(3000, inner.ReadTimeout);
        Assert.Equal(4000, inner.WriteTimeout);
    }

    [Fact]
    public void ChunkedReadStream_ForwardsTheTimeouts_ThroughTheBufferedStream()
    {
        using var inner = new TimeoutCapableStream();
        using var stream = new ChunkedReadStream(new BufferedReadStream(inner, null, 8192, NullLogger.Instance));

        Assert.True(stream.CanTimeout);
        Assert.Equal(1000, stream.ReadTimeout);

        stream.ReadTimeout = 3000;

        Assert.Equal(3000, inner.ReadTimeout);
    }

    [Fact]
    public void ContentLengthReadStream_ForwardsTheTimeouts_OfTheInnerStream()
    {
        using var inner = new TimeoutCapableStream();
        using var stream = new ContentLengthReadStream(inner, 16);

        Assert.True(stream.CanTimeout);
        Assert.Equal(1000, stream.ReadTimeout);

        stream.ReadTimeout = 3000;

        Assert.Equal(3000, inner.ReadTimeout);
    }

    [Fact]
    public void StandardStreamResponse_ForwardsTheTimeouts_OfTheInnerStream()
    {
        using var inner = new TimeoutCapableStream();
        using var response = new HttpResponseMessage();
        using var stream = new StandardStreamResponse(response, inner);

        Assert.True(stream.CanTimeout);
        Assert.Equal(1000, stream.ReadTimeout);
        Assert.Equal(2000, stream.WriteTimeout);

        stream.ReadTimeout = 3000;
        stream.WriteTimeout = 4000;

        Assert.Equal(3000, inner.ReadTimeout);
        Assert.Equal(4000, inner.WriteTimeout);
    }

    [Fact]
    public void HijackedStreamResponse_ForwardsTheTimeouts_OfTheInnerStream()
    {
        using var inner = new TimeoutCapableStream();
        using var response = new HttpResponseMessage();
        using var stream = new HijackedStreamResponse(response, new BufferedReadStream(inner, null, 8192, NullLogger.Instance));

        Assert.True(stream.CanTimeout);
        Assert.Equal(1000, stream.ReadTimeout);
        Assert.Equal(2000, stream.WriteTimeout);

        stream.ReadTimeout = 3000;
        stream.WriteTimeout = 4000;

        Assert.Equal(3000, inner.ReadTimeout);
        Assert.Equal(4000, inner.WriteTimeout);
    }

    [Fact]
    public void BufferedReadStream_DoesNotReportTimeoutSupport_WhenTheInnerStreamHasNone()
    {
        using var inner = new PartialReadStream("abc", 1);
        using var stream = new BufferedReadStream(inner, null, 8192, NullLogger.Instance);

        Assert.False(stream.CanTimeout);
    }
}