namespace Notify.Core;

/// <summary>
/// Defines compression operations for serialized notification payloads.
/// </summary>
public interface IPayloadCompressor
{
    /// <summary>
    /// Compresses the provided payload into a new byte array.
    /// </summary>
    /// <param name="payload">The payload to compress.</param>
    /// <returns>A byte array containing the compressed payload.</returns>
    byte[] Compress(ReadOnlySpan<byte> payload);

    /// <summary>
    /// Decompresses the provided payload into its original byte array form.
    /// </summary>
    /// <param name="payload">The payload to decompress.</param>
    /// <returns>A byte array containing the decompressed payload.</returns>
    byte[] Decompress(ReadOnlySpan<byte> payload);
}
