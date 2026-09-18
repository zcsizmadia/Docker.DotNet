#if !NETSTANDARD2_1_OR_GREATER && !NETCOREAPP2_1_OR_GREATER
namespace Microsoft.Net.Http.Client;

/// <summary>
/// Provides <see cref="Memory{T}"/> based read and write overloads for <see cref="Stream"/> on target
/// frameworks that do not offer them. The stream implementations in this assembly are written against
/// these overloads, so that a single implementation is shared across all target frameworks.
/// </summary>
internal static class StreamExtensions
{
    public static ValueTask<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
        {
            return new ValueTask<int>(stream.ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));
        }

        return ReadUsingPooledBufferAsync(stream, buffer, cancellationToken);
    }

    public static ValueTask WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (MemoryMarshal.TryGetArray(buffer, out var segment))
        {
            return new ValueTask(stream.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));
        }

        return WriteUsingPooledBufferAsync(stream, buffer, cancellationToken);
    }

    private static async ValueTask<int> ReadUsingPooledBufferAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var array = ArrayPool<byte>.Shared.Rent(buffer.Length);

        try
        {
            var readBytesCount = await stream.ReadAsync(array, 0, buffer.Length, cancellationToken)
                .ConfigureAwait(false);

            array.AsSpan(0, readBytesCount).CopyTo(buffer.Span);

            return readBytesCount;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    private static async ValueTask WriteUsingPooledBufferAsync(Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var array = ArrayPool<byte>.Shared.Rent(buffer.Length);

        try
        {
            buffer.Span.CopyTo(array);

            await stream.WriteAsync(array, 0, buffer.Length, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }
}
#endif