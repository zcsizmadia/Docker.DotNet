namespace Docker.DotNet.Tests;

public sealed class ChunkedReadStreamTests
{
    private const string ChunkedBody = "4\r\nWiki\r\n5\r\npedia\r\nE\r\n in\r\n\r\nchunks.\r\n0\r\n\r\n";

    private const string DecodedBody = "Wikipedia in\r\n\r\nchunks.";

    private static ChunkedReadStream CreateStream(Stream inner)
        => new ChunkedReadStream(new BufferedReadStream(inner, null, 8192, NullLogger.Instance));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 1)]
    [InlineData(7, 4)]
    [InlineData(8192, 8192)]
    public async Task ReadAsync_DecodesChunks_WhenSplitAcrossReads(int maxBytesPerRead, int destinationLength)
    {
        using var inner = new PartialReadStream(ChunkedBody, maxBytesPerRead);
        using var stream = CreateStream(inner);

        Assert.Equal(DecodedBody, await ReadToEndAsync(stream, destinationLength));
    }

    [Fact]
    public async Task ReadAsync_DecodesSameBytes_ForByteArrayAndMemoryOverloads()
    {
        using var byteArrayInner = new PartialReadStream(ChunkedBody, 3);
        using var byteArrayStream = CreateStream(byteArrayInner);

        using var memoryInner = new PartialReadStream(ChunkedBody, 3);
        using var memoryStream = CreateStream(memoryInner);

        var fromByteArray = new MemoryStream();
        var fromMemory = new MemoryStream();

        var buffer = new byte[5];

        int read;

        while ((read = await byteArrayStream.ReadAsync(buffer, 0, buffer.Length, TestContext.Current.CancellationToken)) > 0)
        {
            fromByteArray.Write(buffer, 0, read);
        }

        while ((read = await memoryStream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken)) > 0)
        {
            fromMemory.Write(buffer, 0, read);
        }

        Assert.Equal(fromByteArray.ToArray(), fromMemory.ToArray());
        Assert.Equal(DecodedBody, Encoding.ASCII.GetString(fromMemory.ToArray()));
    }

    [Fact]
    public async Task ReadAsync_ReturnsZero_WhenTheBodyIsEmpty()
    {
        using var inner = new PartialReadStream("0\r\n\r\n", 1);
        using var stream = CreateStream(inner);

        var buffer = new byte[16];

        Assert.Equal(0, await stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_ReturnsZero_WhenCalledAfterTheFinalChunk()
    {
        using var inner = new PartialReadStream(ChunkedBody, 8192);
        using var stream = CreateStream(inner);

        await ReadToEndAsync(stream, 64);

        var buffer = new byte[16];

        Assert.Equal(0, await stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_Throws_WhenTheChunkHeaderIsNotHexadecimal()
    {
        using var inner = new PartialReadStream("zz\r\ndata\r\n0\r\n\r\n", 8192);
        using var stream = CreateStream(inner);

        var buffer = new byte[16];

        var exception = await Assert.ThrowsAsync<IOException>(async () =>
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken);
            Assert.Fail($"Expected the read to fail, but it returned {read} bytes.");
        });

        Assert.Contains("Invalid chunk header", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_Throws_WhenTheChunkIsNotTerminatedByAnEmptyLine()
    {
        using var inner = new PartialReadStream("4\r\nWikiXX\r\n0\r\n\r\n", 8192);
        using var stream = CreateStream(inner);

        var buffer = new byte[16];

        await Assert.ThrowsAsync<IOException>(async () =>
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken);
            Assert.Fail($"Expected the read to fail, but it returned {read} bytes.");
        });
    }

    [Fact]
    public async Task ReadAsync_Throws_WhenTheStreamEndsInTheMiddleOfAChunk()
    {
        using var inner = new PartialReadStream("8\r\nWiki", 8192);
        using var stream = CreateStream(inner);

        var buffer = new byte[16];

        // The first read returns the four bytes that did arrive, the next one hits the end of the stream.
        Assert.Equal(4, await stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<EndOfStreamException>(async () =>
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken);
            Assert.Fail($"Expected the read to fail, but it returned {read} bytes.");
        });
    }

    private static async Task<string> ReadToEndAsync(ChunkedReadStream stream, int destinationLength)
    {
        using var body = new MemoryStream();

        var buffer = new byte[destinationLength];

        int read;

        while ((read = await stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken)) > 0)
        {
            body.Write(buffer, 0, read);
        }

        return Encoding.ASCII.GetString(body.ToArray());
    }
}