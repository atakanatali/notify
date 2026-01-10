namespace Notify.Broker.Abstractions;

/// <summary>
/// Defines a broker-agnostic client that can publish and consume messages.
/// </summary>
public interface IBrokerClient : IBrokerPublisher, IBrokerConsumer
{
}
