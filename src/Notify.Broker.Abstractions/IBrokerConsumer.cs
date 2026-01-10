namespace Notify.Broker.Abstractions;

/// <summary>
/// Provides a broker-agnostic contract for consuming messages from a destination.
/// </summary>
public interface IBrokerConsumer
{
    /// <summary>
    /// Consumes messages from the specified destination using the provided handler and options.
    /// </summary>
    /// <param name="destination">The broker destination (queue, topic, or exchange) to consume from.</param>
    /// <param name="handler">The asynchronous handler to invoke for each received message.</param>
    /// <param name="options">The consumption settings that control concurrency, prefetching, and batching.</param>
    /// <param name="ct">The cancellation token used to stop consuming messages.</param>
    /// <returns>A task that completes when consumption has stopped.</returns>
    Task ConsumeAsync(
        string destination,
        Func<BrokerMessage, CancellationToken, Task> handler,
        BrokerConsumeOptions options,
        CancellationToken ct);
}
