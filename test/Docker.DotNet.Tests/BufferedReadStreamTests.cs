namespace Docker.DotNet.Tests;

public sealed class BufferedReadStreamTests
{
    private static BufferedReadStream CreateStream(Stream inner, int bufferLength = 8192)
        => new BufferedReadStream(inner, null, bufferLength, NullLogger.Instance);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8192)]
    public async Task ReadLineAsync_ReadsAllLines_WhenLineBreaksSplitAcrossReads(int maxBytesPerRead)
    {
        const string payload = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n";

        using var inner = new PartialReadStream(payload, maxBytesPerRead);
        using var stream = CreateStream(inner);

        Assert.Equal("HTTP/1.1 200 OK", await stream.ReadLineAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Content-Length: 2", await stream.ReadLineAsync(TestContext.Current.CancellationToken));
        Assert.Equal(string.Empty, await stream.ReadLineAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadLineAsync_ReturnsNull_WhenStreamIsEmpty()
    {
        using var inner = new PartialReadStream(string.Empty, 1);
        using var stream = CreateStream(inner);

        Assert.Null(await stream.ReadLineAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadLineAsync_KeepsCarriageReturn_WhenNotFollowedByLineFeed()
    {
        using var inner = new PartialReadStream("a\rb\r\n", 1);
        using var stream = CreateStream(inner);

        Assert.Equal("a\rb", await stream.ReadLineAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadLineAsync_ReturnsTrailingData_WhenStreamEndsWithoutLineBreak()
    {
        using var inner = new PartialReadStream("no line break", 4);
        using var stream = CreateStream(inner);

        Assert.Equal("no line break", await stream.ReadLineAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_CompletesSynchronously_WhenBytesAreAlreadyBuffered()
    {
        using var inner = new PartialReadStream("header\r\nbody", 64);
        using var stream = CreateStream(inner);

        // Reading the line leaves the remainder of the payload in the internal buffer.
        Assert.Equal("header", await stream.ReadLineAsync(TestContext.Current.CancellationToken));

        var buffer = new byte[4];
        var readTask = stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken);

        Assert.True(readTask.IsCompletedSuccessfully);
        Assert.Equal(4, await readTask);
        Assert.Equal("body", Encoding.ASCII.GetString(buffer));
    }

    [Fact]
    public async Task ReadAsync_ReadsSameBytes_ForByteArrayAndMemoryOverloads()
    {
        const string payload = "header\r\nthe quick brown fox";

        using var byteArrayInner = new PartialReadStream(payload, 3);
        using var byteArrayStream = CreateStream(byteArrayInner);

        using var memoryInner = new PartialReadStream(payload, 3);
        using var memoryStream = CreateStream(memoryInner);

        await byteArrayStream.ReadLineAsync(TestContext.Current.CancellationToken);
        await memoryStream.ReadLineAsync(TestContext.Current.CancellationToken);

        var fromByteArray = new byte[19];
        var fromMemory = new byte[19];

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

        Assert.Equal(19, byteArrayCount);
        Assert.Equal(byteArrayCount, memoryCount);
        Assert.Equal(fromByteArray, fromMemory);
        Assert.Equal("the quick brown fox", Encoding.ASCII.GetString(fromMemory));
    }

    [Fact]
    public void Read_ReadsBufferedBytes_BeforeReadingTheInnerStream()
    {
        using var inner = new PartialReadStream("abcdef", 6);
        using var stream = CreateStream(inner);

        var buffer = new byte[6];

        Assert.Equal(6, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal("abcdef", Encoding.ASCII.GetString(buffer));
    }

    [Fact]
    public async Task DisposeAsync_DisposesTheInnerStreamAsynchronously()
    {
        var inner = new PartialReadStream("abc", 1);
        var stream = CreateStream(inner);

        await stream.DisposeAsync();

        Assert.True(inner.IsDisposedAsynchronously);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent_WhenFollowedByDispose()
    {
        var inner = new PartialReadStream("abc", 1);
        var stream = CreateStream(inner);

        // The pooled read buffer must not be returned twice, which would hand the same array
        // out to two different callers.
        await stream.DisposeAsync();
        stream.Dispose();

        Assert.True(inner.IsDisposed);
    }
}