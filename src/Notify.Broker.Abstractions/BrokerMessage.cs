namespace Notify.Broker.Abstractions;

/// <summary>
/// Represents a broker-agnostic message payload and metadata for publishing or consumption.
/// </summary>
public sealed class BrokerMessage
{
    /// <summary>
    /// Gets or sets the raw message payload bytes.
    /// </summary>
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the optional correlation identifier used for tracing across systems.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the optional unique message identifier assigned by the producer or broker.
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp indicating when the message was created.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
