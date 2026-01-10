using Microsoft.Extensions.Options;
using Notify.Abstractions;
using Notify.Broker.Abstractions;

namespace Notify.Core;

/// <summary>
/// Publishes encoded notification packages to broker destinations for downstream dispatching.
/// </summary>
public sealed class Notifier : INotify
{
    private const string EmailChannelName = "email";
    private const string SmsChannelName = "sms";
    private const string PushChannelName = "push";

    private readonly IBrokerPublisher brokerPublisher;
    private readonly NotificationCodec codec;
    private readonly NotifyOptions options;
    private readonly string queuePrefix;
    private readonly string emailDestination;
    private readonly string smsDestination;
    private readonly string pushDestination;
    private readonly int batchSize;
    private readonly int maxInFlight;

    /// <summary>
    /// Initializes a new instance of the <see cref="Notifier"/> class.
    /// </summary>
    /// <param name="brokerPublisher">The broker publisher used to send messages.</param>
    /// <param name="codec">The codec used to encode notification packages.</param>
    /// <param name="options">The configuration snapshot that controls publishing behavior.</param>
    public Notifier(
        IBrokerPublisher brokerPublisher,
        NotificationCodec codec,
        IOptions<NotifyOptions> options)
    {
        this.brokerPublisher = brokerPublisher ?? throw new ArgumentNullException(nameof(brokerPublisher));
        this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        this.options = options.Value;
        queuePrefix = ResolveQueuePrefix(this.options);
        emailDestination = BrokerNaming.BuildQueueName(queuePrefix, EmailChannelName);
        smsDestination = BrokerNaming.BuildQueueName(queuePrefix, SmsChannelName);
        pushDestination = BrokerNaming.BuildQueueName(queuePrefix, PushChannelName);
        batchSize = Math.Max(1, this.options.Publishing.BatchSize);
        maxInFlight = this.options.Publishing.MaxInFlight > 0 ? this.options.Publishing.MaxInFlight : batchSize;
    }

    /// <summary>
    /// Sends a single notification package to the configured broker destination.
    /// </summary>
    /// <param name="package">The notification package to publish.</param>
    /// <param name="ct">The cancellation token used to abort the operation.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    public Task SendAsync(NotificationPackage package, CancellationToken ct = default)
    {
        if (package is null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        ct.ThrowIfCancellationRequested();
        string destination = ResolveDestination(package.Channel);
        BrokerMessage message = EncodeMessage(package);
        return brokerPublisher.PublishAsync(destination, message, ct);
    }

    /// <summary>
    /// Sends a batch of notification packages to the configured broker destinations.
    /// </summary>
    /// <param name="packages">The notification packages to publish.</param>
    /// <param name="ct">The cancellation token used to abort the operation.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    public async Task SendBatchAsync(IReadOnlyList<NotificationPackage> packages, CancellationToken ct = default)
    {
        if (packages is null)
        {
            throw new ArgumentNullException(nameof(packages));
        }

        if (packages.Count == 0)
        {
            return;
        }

        if (batchSize <= 1 && maxInFlight <= 1)
        {
            foreach (NotificationPackage package in packages)
            {
                ct.ThrowIfCancellationRequested();
                await SendAsync(package, ct).ConfigureAwait(false);
            }

            return;
        }

        List<Task> inFlight = new(Math.Min(maxInFlight, packages.Count));

        foreach (NotificationPackage package in packages)
        {
            ct.ThrowIfCancellationRequested();
            inFlight.Add(PublishPackageAsync(package, ct));

            if (inFlight.Count >= batchSize || inFlight.Count >= maxInFlight)
            {
                await Task.WhenAll(inFlight).ConfigureAwait(false);
                inFlight.Clear();
            }
        }

        if (inFlight.Count > 0)
        {
            await Task.WhenAll(inFlight).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Publishes an encoded notification package to the appropriate broker destination.
    /// </summary>
    /// <param name="package">The notification package to publish.</param>
    /// <param name="ct">The cancellation token used to abort the operation.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    private Task PublishPackageAsync(NotificationPackage package, CancellationToken ct)
    {
        string destination = ResolveDestination(package.Channel);
        BrokerMessage message = EncodeMessage(package);
        return brokerPublisher.PublishAsync(destination, message, ct);
    }

    /// <summary>
    /// Encodes the notification package into a broker message using configured serialization and compression.
    /// </summary>
    /// <param name="package">The notification package to encode.</param>
    /// <returns>The broker message containing the encoded payload.</returns>
    private BrokerMessage EncodeMessage(NotificationPackage package)
    {
        byte[] payload = codec.Serialize(package, options.Serialization, options.Compression);
        return new BrokerMessage
        {
            Payload = payload,
            CorrelationId = package.CorrelationId
        };
    }

    /// <summary>
    /// Resolves the broker destination name for the specified notification channel.
    /// </summary>
    /// <param name="channel">The notification channel to resolve.</param>
    /// <returns>The broker destination name for the channel.</returns>
    private string ResolveDestination(NotificationChannel channel)
    {
        return channel switch
        {
            NotificationChannel.Email => emailDestination,
            NotificationChannel.Sms => smsDestination,
            NotificationChannel.Push => pushDestination,
            _ => throw new InvalidOperationException("Unsupported notification channel.")
        };
    }

    /// <summary>
    /// Resolves the queue prefix from the configured options.
    /// </summary>
    /// <param name="currentOptions">The current options snapshot.</param>
    /// <returns>The resolved queue prefix.</returns>
    private static string ResolveQueuePrefix(NotifyOptions currentOptions)
    {
        string? prefix = currentOptions.QueuePrefix;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new InvalidOperationException("Queue prefix must be configured before sending notifications.");
        }

        return prefix;
    }
}
