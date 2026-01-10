namespace Notify.Core;

/// <summary>
/// Provides a no-op compressor that returns payloads without modification.
/// </summary>
public sealed class NoCompression : IPayloadCompressor
{
    /// <summary>
    /// Returns the provided payload as a new byte array without compression.
    /// </summary>
    /// <param name="payload">The payload to return.</param>
    /// <returns>A byte array containing the original payload.</returns>
    public byte[] Compress(ReadOnlySpan<byte> payload)
    {
        return payload.ToArray();
    }

    /// <summary>
    /// Returns the provided payload as a new byte array without decompression.
    /// </summary>
    /// <param name="payload">The payload to return.</param>
    /// <returns>A byte array containing the original payload.</returns>
    public byte[] Decompress(ReadOnlySpan<byte> payload)
    {
        return payload.ToArray();
    }
}
