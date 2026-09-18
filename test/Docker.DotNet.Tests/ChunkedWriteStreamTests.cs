namespace Docker.DotNet.Tests;

public sealed class ChunkedWriteStreamTests
{
    [Fact]
    public async Task WriteAsync_FramesThePayload_WithTheChunkSizeAndTrailingLineBreak()
    {
        using var inner = new RecordingStream();
        using var stream = new ChunkedWriteStream(inner);

        await stream.WriteAsync("Wiki"u8.ToArray().AsMemory(), TestContext.Current.CancellationToken);

        Assert.Equal("4\r\nWiki\r\n", inner.ToAsciiString());
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(10, "A")]
    [InlineData(15, "F")]
    [InlineData(16, "10")]
    [InlineData(255, "FF")]
    [InlineData(4096, "1000")]
    [InlineData(1048576, "100000")]
    public async Task WriteAsync_WritesTheChunkSize_AsUppercaseHexadecimal(int chunkLength, string expectedChunkSize)
    {
        using var inner = new RecordingStream();
        using var stream = new ChunkedWriteStream(inner);

        await stream.WriteAsync(new byte[chunkLength].AsMemory(), TestContext.Current.CancellationToken);

        var written = inner.ToAsciiString();

        Assert.StartsWith(expectedChunkSize + "\r\n", written);
        Assert.EndsWith("\r\n", written);
        Assert.Equal(expectedChunkSize.Length + 2 + chunkLength + 2, written.Length);
    }

    [Fact]
    public async Task WriteAsync_WritesNothing_WhenThePayloadIsEmpty()
    {
        using var inner = new RecordingStream();
        using var stream = new ChunkedWriteStream(inner);

        await stream.WriteAsync(Array.Empty<byte>().AsMemory(), TestContext.Current.CancellationToken);

        Assert.Empty(inner.ToArray());
    }

    [Fact]
    public async Task WriteAsync_WritesTheSameBytes_ForByteArrayAndMemoryOverloads()
    {
        var payload = Encoding.ASCII.GetBytes("the quick brown fox jumps over the lazy dog");

        using var byteArrayInner = new RecordingStream();
        using var byteArrayStream = new ChunkedWriteStream(byteArrayInner);

        using var memoryInner = new RecordingStream();
        using var memoryStream = new ChunkedWriteStream(memoryInner);

        await byteArrayStream.WriteAsync(payload, 0, payload.Length, TestContext.Current.CancellationToken);
        await memoryStream.WriteAsync(payload.AsMemory(), TestContext.Current.CancellationToken);

        Assert.Equal(byteArrayInner.ToArray(), memoryInner.ToArray());
        Assert.Equal("2B\r\nthe quick brown fox jumps over the lazy dog\r\n", memoryInner.ToAsciiString());
    }

    [Fact]
    public async Task WriteAsync_HonoursTheOffsetAndCount_OfTheByteArrayOverload()
    {
        var payload = Encoding.ASCII.GetBytes("xxWikixx");

        using var inner = new RecordingStream();
        using var stream = new ChunkedWriteStream(inner);

        await stream.WriteAsync(payload, 2, 4, TestContext.Current.CancellationToken);

        Assert.Equal("4\r\nWiki\r\n", inner.ToAsciiString());
    }

    [Fact]
    public async Task WriteAsync_FramesEachPayload_WhenCalledRepeatedly()
    {
        using var inner = new RecordingStream();
        using var stream = new ChunkedWriteStream(inner);

        await stream.WriteAsync("Wiki"u8.ToArray().AsMemory(), TestContext.Current.CancellationToken);
        await stream.WriteAsync("pedia"u8.ToArray().AsMemory(), TestContext.Current.CancellationToken);

        Assert.Equal("4\r\nWiki\r\n5\r\npedia\r\n", inner.ToAsciiString());
    }

    [Fact]
    public async Task EndContentAsync_WritesTheTerminatingChunk()
    {
        using var inner = new RecordingStream();
        using var stream = new ChunkedWriteStream(inner);

        await stream.WriteAsync("Wiki"u8.ToArray().AsMemory(), TestContext.Current.CancellationToken);
        await stream.EndContentAsync(TestContext.Current.CancellationToken);

        Assert.Equal("4\r\nWiki\r\n0\r\n\r\n", inner.ToAsciiString());
    }

    [Fact]
    public async Task EndContentAsync_WritesTheTerminatingChunk_WhenNothingWasWritten()
    {
        using var inner = new RecordingStream();
        using var stream = new ChunkedWriteStream(inner);

        await stream.EndContentAsync(TestContext.Current.CancellationToken);

        Assert.Equal("0\r\n\r\n", inner.ToAsciiString());
    }

    [Fact]
    public async Task WriteAsync_ProducesABody_ThatChunkedReadStreamDecodes()
    {
        var payload = Encoding.ASCII.GetBytes(new string('a', 300) + new string('b', 17));

        using var inner = new RecordingStream();
        using var writeStream = new ChunkedWriteStream(inner);

        await writeStream.WriteAsync(payload.AsMemory(0, 300), TestContext.Current.CancellationToken);
        await writeStream.WriteAsync(payload.AsMemory(300), TestContext.Current.CancellationToken);
        await writeStream.EndContentAsync(TestContext.Current.CancellationToken);

        using var encoded = new PartialReadStream(inner.ToArray(), 13);
        using var readStream = new ChunkedReadStream(new BufferedReadStream(encoded, null, 8192, NullLogger.Instance));

        using var decoded = new MemoryStream();

        var buffer = new byte[64];

        int read;

        while ((read = await readStream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken)) > 0)
        {
            decoded.Write(buffer, 0, read);
        }

        Assert.Equal(payload, decoded.ToArray());
    }
}