namespace Notify.Broker.Abstractions;

/// <summary>
/// Provides helper methods for generating broker destination names.
/// </summary>
public static class BrokerNaming
{
    /// <summary>
    /// Builds a queue name using the provided prefix and channel, normalized to lowercase.
    /// </summary>
    /// <param name="prefix">The prefix used to namespace the queue name.</param>
    /// <param name="channel">The channel name segment such as email, sms, or push.</param>
    /// <returns>The formatted queue name in the form "{prefix}.{channel}".</returns>
    /// <exception cref="ArgumentException">Thrown when prefix or channel is null or whitespace.</exception>
    public static string BuildQueueName(string prefix, string channel)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Queue prefix cannot be null or whitespace.", nameof(prefix));
        }

        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new ArgumentException("Queue channel cannot be null or whitespace.", nameof(channel));
        }

        return $"{prefix}.{channel.Trim().ToLowerInvariant()}";
    }
}
