namespace Notify.Broker.Abstractions;

/// <summary>
/// Defines broker-agnostic consumption settings for concurrency, prefetching, and batching.
/// </summary>
public sealed class BrokerConsumeOptions
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent message handlers to run for the destination.
    /// </summary>
    public int Concurrency { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of messages to prefetch from the broker at a time.
    /// </summary>
    public int Prefetch { get; set; }

    /// <summary>
    /// Gets or sets the batch size to use when grouping messages for processing.
    /// </summary>
    public int BatchSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum time in milliseconds to wait before dispatching a partial batch.
    /// </summary>
    public int BatchMaxWaitMs { get; set; }
}
