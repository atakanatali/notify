using System.Diagnostics.Metrics;

namespace Notify.Abstractions;

/// <summary>
/// Provides shared metrics for notification publishing and dispatching operations.
/// </summary>
public static class NotifyMetrics
{
    private static readonly Meter Meter = new("Notify", "1.0.0");
    private static readonly Counter<long> PublishedTotal =
        Meter.CreateCounter<long>("notify_published_total");
    private static readonly Counter<long> ConsumedTotal =
        Meter.CreateCounter<long>("notify_consumed_total");
    private static readonly Counter<long> SentTotal =
        Meter.CreateCounter<long>("notify_sent_total");
    private static readonly Counter<long> FailedTotal =
        Meter.CreateCounter<long>("notify_failed_total");
    private static readonly Counter<long> RetryTotal =
        Meter.CreateCounter<long>("notify_retry_total");
    private static readonly Histogram<double> SendLatencyMs =
        Meter.CreateHistogram<double>("notify_send_latency_ms");

    /// <summary>
    /// Records a published notification count for the specified channel.
    /// </summary>
    /// <param name="channel">The notification channel associated with the publish operation.</param>
    /// <param name="count">The number of notifications published.</param>
    public static void RecordPublished(NotificationChannel channel, int count = 1)
    {
        if (count <= 0)
        {
            return;
        }

        PublishedTotal.Add(count, CreateTags(channel, provider: null));
    }

    /// <summary>
    /// Records a consumed notification count for the specified channel.
    /// </summary>
    /// <param name="channel">The notification channel associated with the consumed message.</param>
    /// <param name="count">The number of notifications consumed.</param>
    public static void RecordConsumed(NotificationChannel channel, int count = 1)
    {
        if (count <= 0)
        {
            return;
        }

        ConsumedTotal.Add(count, CreateTags(channel, provider: null));
    }

    /// <summary>
    /// Records a sent notification count for the specified channel.
    /// </summary>
    /// <param name="channel">The notification channel associated with the send operation.</param>
    /// <param name="count">The number of notifications sent.</param>
    /// <param name="provider">The provider name handling the send operation.</param>
    public static void RecordSent(NotificationChannel channel, int count, string? provider)
    {
        if (count <= 0)
        {
            return;
        }

        SentTotal.Add(count, CreateTags(channel, provider));
    }

    /// <summary>
    /// Records a failed notification count for the specified channel.
    /// </summary>
    /// <param name="channel">The notification channel associated with the failed send.</param>
    /// <param name="count">The number of failed notifications.</param>
    /// <param name="provider">The provider name handling the send operation.</param>
    public static void RecordFailed(NotificationChannel channel, int count, string? provider)
    {
        if (count <= 0)
        {
            return;
        }

        FailedTotal.Add(count, CreateTags(channel, provider));
    }

    /// <summary>
    /// Records a retry attempt for the specified channel.
    /// </summary>
    /// <param name="channel">The notification channel associated with the retry attempt.</param>
    /// <param name="count">The number of retries recorded.</param>
    /// <param name="provider">The provider name handling the send operation.</param>
    public static void RecordRetry(NotificationChannel channel, int count, string? provider)
    {
        if (count <= 0)
        {
            return;
        }

        RetryTotal.Add(count, CreateTags(channel, provider));
    }

    /// <summary>
    /// Records the latency, in milliseconds, for a send operation.
    /// </summary>
    /// <param name="channel">The notification channel associated with the send.</param>
    /// <param name="provider">The provider name handling the send operation.</param>
    /// <param name="latencyMs">The elapsed latency in milliseconds.</param>
    public static void RecordSendLatency(NotificationChannel channel, string? provider, double latencyMs)
    {
        if (latencyMs < 0)
        {
            return;
        }

        SendLatencyMs.Record(latencyMs, CreateTags(channel, provider));
    }

    /// <summary>
    /// Creates a tag list for the specified channel and provider.
    /// </summary>
    /// <param name="channel">The notification channel.</param>
    /// <param name="provider">The provider name, when available.</param>
    /// <returns>A <see cref="TagList"/> populated with channel and provider tags.</returns>
    private static TagList CreateTags(NotificationChannel channel, string? provider)
    {
        TagList tags = new()
        {
            { "channel", channel.ToString().ToLowerInvariant() }
        };

        if (!string.IsNullOrWhiteSpace(provider))
        {
            tags.Add("provider", provider);
        }

        return tags;
    }
}
