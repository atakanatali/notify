using Notify.Broker.Abstractions;

namespace Notify.Core;

/// <summary>
/// Defines configuration options for the notification pipeline, including serialization,
/// compression, publishing, and consumer concurrency settings.
/// </summary>
public sealed class NotifyOptions
{
    /// <summary>
    /// Gets or sets the queue prefix to use when naming broker destinations.
    /// When unset, the application name is resolved and applied later.
    /// </summary>
    public string? QueuePrefix { get; set; }

    /// <summary>
    /// Gets or sets the serialization format used for notification payloads.
    /// </summary>
    public NotifySerialization Serialization { get; set; } = NotifySerialization.Json;

    /// <summary>
    /// Gets the compression settings applied to serialized notification payloads.
    /// </summary>
    public NotifyCompressionOptions Compression { get; } = new();

    /// <summary>
    /// Gets the publishing settings that control batching and in-flight limits.
    /// </summary>
    public NotifyPublishingOptions Publishing { get; } = new();

    /// <summary>
    /// Gets the broker consumption settings for email notifications.
    /// </summary>
    public NotifyConsumerOptions Email { get; } = new();

    /// <summary>
    /// Gets the broker consumption settings for SMS notifications.
    /// </summary>
    public NotifyConsumerOptions Sms { get; } = new();

    /// <summary>
    /// Gets the broker consumption settings for push notifications.
    /// </summary>
    public NotifyConsumerOptions Push { get; } = new();
}

/// <summary>
/// Enumerates the supported serialization formats for notification payloads.
/// </summary>
public enum NotifySerialization
{
    /// <summary>
    /// Uses System.Text.Json to serialize notification payloads.
    /// </summary>
    Json,

    /// <summary>
    /// Uses MessagePack to serialize notification payloads.
    /// </summary>
    MessagePack
}

/// <summary>
/// Defines compression settings for notification payloads.
/// </summary>
public sealed class NotifyCompressionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether compression is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the compression algorithm to apply when compression is enabled.
    /// </summary>
    public NotifyCompressionAlgorithm Algorithm { get; set; } = NotifyCompressionAlgorithm.None;
}

/// <summary>
/// Enumerates the supported compression algorithms for notification payloads.
/// </summary>
public enum NotifyCompressionAlgorithm
{
    /// <summary>
    /// Disables compression and stores the payload as-is.
    /// </summary>
    None,

    /// <summary>
    /// Uses LZ4 for fast compression and decompression.
    /// </summary>
    Lz4
}

/// <summary>
/// Defines publishing settings for outgoing notification batches.
/// </summary>
public sealed class NotifyPublishingOptions
{
    /// <summary>
    /// Gets or sets the maximum number of notifications to include in a batch.
    /// </summary>
    public int BatchSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of in-flight notifications allowed.
    /// </summary>
    public int MaxInFlight { get; set; }
}

/// <summary>
/// Defines broker consumption settings that mirror broker-agnostic options.
/// </summary>
public sealed class NotifyConsumerOptions
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent handlers for the consumer.
    /// </summary>
    public int Concurrency { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of messages to prefetch from the broker.
    /// </summary>
    public int Prefetch { get; set; }

    /// <summary>
    /// Gets or sets the number of messages to collect in a processing batch.
    /// </summary>
    public int BatchSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum time in milliseconds to wait before dispatching a partial batch.
    /// </summary>
    public int BatchMaxWaitMs { get; set; }

    /// <summary>
    /// Creates a broker consume options instance that mirrors this configuration.
    /// </summary>
    /// <returns>A populated <see cref="BrokerConsumeOptions"/> instance.</returns>
    public BrokerConsumeOptions ToBrokerOptions()
    {
        return new BrokerConsumeOptions
        {
            Concurrency = Concurrency,
            Prefetch = Prefetch,
            BatchSize = BatchSize,
            BatchMaxWaitMs = BatchMaxWaitMs
        };
    }
}
