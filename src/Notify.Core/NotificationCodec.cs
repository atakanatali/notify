using Notify.Abstractions;

namespace Notify.Core;

/// <summary>
/// Encodes and decodes notification packages with headers that describe serialization
/// and compression settings.
/// </summary>
public sealed class NotificationCodec
{
    /// <summary>
    /// The number of header bytes written before the payload.
    /// </summary>
    public const int HeaderLength = 3;

    /// <summary>
    /// The current header version written by this codec.
    /// </summary>
    public const byte HeaderVersion = 1;

    private readonly INotificationSerializer _jsonSerializer;
    private readonly INotificationSerializer _messagePackSerializer;
    private readonly IPayloadCompressor _noCompression;
    private readonly IPayloadCompressor _lz4Compression;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationCodec"/> class.
    /// </summary>
    /// <param name="jsonSerializer">The JSON serializer to use when requested.</param>
    /// <param name="messagePackSerializer">The MessagePack serializer to use when requested.</param>
    /// <param name="noCompression">The no-op compressor for uncompressed payloads.</param>
    /// <param name="lz4Compression">The LZ4 compressor for compressed payloads.</param>
    public NotificationCodec(
        INotificationSerializer jsonSerializer,
        INotificationSerializer messagePackSerializer,
        IPayloadCompressor noCompression,
        IPayloadCompressor lz4Compression)
    {
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
        _messagePackSerializer = messagePackSerializer ?? throw new ArgumentNullException(nameof(messagePackSerializer));
        _noCompression = noCompression ?? throw new ArgumentNullException(nameof(noCompression));
        _lz4Compression = lz4Compression ?? throw new ArgumentNullException(nameof(lz4Compression));
    }

    /// <summary>
    /// Serializes and compresses the provided notification package into a payload with a header.
    /// </summary>
    /// <param name="package">The notification package to encode.</param>
    /// <param name="serialization">The serialization format to use.</param>
    /// <param name="compression">The compression settings to apply.</param>
    /// <returns>A byte array containing the header and payload.</returns>
    public byte[] Serialize(
        NotificationPackage package,
        NotifySerialization serialization,
        NotifyCompressionOptions compression)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(compression);

        var serializer = GetSerializer(serialization);
        var payload = serializer.Serialize(package);
        var compressionAlgorithm = compression.Enabled ? compression.Algorithm : NotifyCompressionAlgorithm.None;
        var compressor = GetCompressor(compressionAlgorithm);
        var compressedPayload = compressor.Compress(payload);

        var result = new byte[HeaderLength + compressedPayload.Length];
        result[0] = HeaderVersion;
        result[1] = (byte)serialization;
        result[2] = (byte)compressionAlgorithm;
        compressedPayload.CopyTo(result.AsSpan(HeaderLength));
        return result;
    }

    /// <summary>
    /// Decompresses and deserializes the provided payload into a notification package.
    /// </summary>
    /// <param name="payload">The payload containing the header and encoded data.</param>
    /// <returns>The decoded notification package.</returns>
    public NotificationPackage Deserialize(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HeaderLength)
        {
            throw new InvalidOperationException("Payload is too short to contain a valid header.");
        }

        if (payload[0] != HeaderVersion)
        {
            throw new InvalidOperationException("Unsupported notification payload header version.");
        }

        var serialization = (NotifySerialization)payload[1];
        var compression = (NotifyCompressionAlgorithm)payload[2];
        var serializer = GetSerializer(serialization);
        var compressor = GetCompressor(compression);
        var compressedPayload = payload.Slice(HeaderLength);
        var decompressed = compressor.Decompress(compressedPayload);
        return serializer.Deserialize(decompressed);
    }

    private INotificationSerializer GetSerializer(NotifySerialization serialization)
    {
        return serialization switch
        {
            NotifySerialization.Json => _jsonSerializer,
            NotifySerialization.MessagePack => _messagePackSerializer,
            _ => throw new InvalidOperationException("Unsupported notification serialization.")
        };
    }

    private IPayloadCompressor GetCompressor(NotifyCompressionAlgorithm algorithm)
    {
        return algorithm switch
        {
            NotifyCompressionAlgorithm.None => _noCompression,
            NotifyCompressionAlgorithm.Lz4 => _lz4Compression,
            _ => throw new InvalidOperationException("Unsupported notification compression.")
        };
    }
}
