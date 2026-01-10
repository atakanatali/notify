namespace Notify.Broker.Abstractions;

/// <summary>
/// Provides a broker-agnostic contract for publishing messages to a destination.
/// </summary>
public interface IBrokerPublisher
{
    /// <summary>
    /// Publishes a message to the specified destination.
    /// </summary>
    /// <param name="destination">The broker destination (queue, topic, or exchange) to publish to.</param>
    /// <param name="message">The message payload and metadata to publish.</param>
    /// <param name="ct">The cancellation token used to abort the publish operation.</param>
    /// <returns>A task that completes when the message has been accepted for publishing.</returns>
    Task PublishAsync(string destination, BrokerMessage message, CancellationToken ct = default);
}
