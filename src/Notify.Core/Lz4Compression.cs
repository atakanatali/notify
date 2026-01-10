using System;
using System.Buffers;
using K4os.Compression.LZ4;

namespace Notify.Core;

/// <summary>
/// Compresses payloads using the LZ4 algorithm for fast throughput.
/// </summary>
public sealed class Lz4Compression : IPayloadCompressor
{
    /// <summary>
    /// Compresses the provided payload using LZ4 with a size-prefixed format.
    /// </summary>
    /// <param name="payload">The payload to compress.</param>
    /// <returns>A byte array containing the compressed payload.</returns>
    public byte[] Compress(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        var maxLength = LZ4Codec.MaximumOutputSize(payload.Length);
        var buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            var encoded = LZ4Codec.Encode(payload, buffer);
            if (encoded <= 0)
            {
                return Array.Empty<byte>();
            }

            var result = new byte[encoded + sizeof(int)];
            BitConverter.TryWriteBytes(result.AsSpan(0, sizeof(int)), payload.Length);
            buffer.AsSpan(0, encoded).CopyTo(result.AsSpan(sizeof(int)));
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Decompresses the provided LZ4 payload into its original form.
    /// </summary>
    /// <param name="payload">The payload to decompress.</param>
    /// <returns>A byte array containing the decompressed payload.</returns>
    public byte[] Decompress(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        if (payload.Length < sizeof(int))
        {
            throw new InvalidOperationException("Compressed payload is missing the decompressed length header.");
        }

        var decodedLength = BitConverter.ToInt32(payload.Slice(0, sizeof(int)));
        if (decodedLength < 0)
        {
            throw new InvalidOperationException("Compressed payload has an invalid decoded length.");
        }

        var output = new byte[decodedLength];
        var decoded = LZ4Codec.Decode(payload.Slice(sizeof(int)), output);
        if (decoded != decodedLength)
        {
            throw new InvalidOperationException("Compressed payload did not decode to the expected length.");
        }

        return output;
    }
}
