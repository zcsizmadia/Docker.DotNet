namespace Docker.DotNet.Tests;

public sealed class ContentLengthReadStreamTests
{
    [Fact]
    public async Task ReadAsync_StopsAtTheContentLength_WhenMoreBytesAreAvailable()
    {
        using var inner = new PartialReadStream("bodyAndThenSomeTrailingBytes", 64);
        using var stream = new ContentLengthReadStream(inner, 4);

        var buffer = new byte[64];

        Assert.Equal(4, await stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken));
        Assert.Equal("body", Encoding.ASCII.GetString(buffer, 0, 4));
        Assert.Equal(0, await stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(64)]
    public async Task ReadAsync_ReadsTheWholeBody_WhenSplitAcrossReads(int maxBytesPerRead)
    {
        const string payload = "the quick brown fox";

        using var inner = new PartialReadStream(payload + "ignored", maxBytesPerRead);
        using var stream = new ContentLengthReadStream(inner, payload.Length);

        using var body = new MemoryStream();

        var buffer = new byte[8];

        int read;

        while ((read = await stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken)) > 0)
        {
            body.Write(buffer, 0, read);
        }

        Assert.Equal(payload, Encoding.ASCII.GetString(body.ToArray()));
    }

    [Fact]
    public async Task ReadAsync_ReadsTheSameBytes_ForByteArrayAndMemoryOverloads()
    {
        const string payload = "the quick brown fox";

        using var byteArrayInner = new PartialReadStream(payload, 3);
        using var byteArrayStream = new ContentLengthReadStream(byteArrayInner, payload.Length);

        using var memoryInner = new PartialReadStream(payload, 3);
        using var memoryStream = new ContentLengthReadStream(memoryInner, payload.Length);

        var fromByteArray = new byte[payload.Length];
        var fromMemory = new byte[payload.Length];

        var byteArrayCount = 0;
        var memoryCount = 0;

        int read;

        while ((read = await byteArrayStream.ReadAsync(fromByteArray, byteArrayCount, fromByteArray.Length - byteArrayCount, TestContext.Current.CancellationToken)) > 0)
        {
            byteArrayCount += read;
        }

        while ((read = await memoryStream.ReadAsync(fromMemory.AsMemory(memoryCount), TestContext.Current.CancellationToken)) > 0)
        {
            memoryCount += read;
        }

        Assert.Equal(byteArrayCount, memoryCount);
        Assert.Equal(fromByteArray, fromMemory);
        Assert.Equal(payload, Encoding.ASCII.GetString(fromMemory));
    }

    [Fact]
    public void Read_Span_StopsAtTheContentLength()
    {
        using var inner = new PartialReadStream("bodyAndThenSomeTrailingBytes", 64);
        using var stream = new ContentLengthReadStream(inner, 4);

        Span<byte> buffer = stackalloc byte[64];

        Assert.Equal(4, stream.Read(buffer));
        Assert.Equal(0, stream.Read(buffer));
    }

    [Fact]
    public async Task ReadAsync_ReturnsZero_WhenTheContentLengthIsZero()
    {
        using var inner = new PartialReadStream("trailing", 64);
        using var stream = new ContentLengthReadStream(inner, 0);

        var buffer = new byte[64];

        Assert.Equal(0, await stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_Throws_WhenCancelled()
    {
        using var inner = new PartialReadStream("body", 64);
        using var stream = new ContentLengthReadStream(inner, 4);

        using var cancellationTokenSource = new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();

        var buffer = new byte[64];

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationTokenSource.Token);
            Assert.Fail($"Expected the read to be cancelled, but it returned {read} bytes.");
        });
    }

    [Fact]
    public async Task DisposeAsync_DisposesTheInnerStreamAsynchronously()
    {
        var inner = new PartialReadStream("body", 64);
        var stream = new ContentLengthReadStream(inner, 4);

        await stream.DisposeAsync();

        Assert.True(inner.IsDisposedAsynchronously);
    }

    [Fact]
    public void Dispose_DisposesTheInnerStream()
    {
        var inner = new PartialReadStream("body", 64);
        var stream = new ContentLengthReadStream(inner, 4);

        stream.Dispose();

        Assert.True(inner.IsDisposed);
        Assert.False(inner.IsDisposedAsynchronously);
    }
}