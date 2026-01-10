using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notify.Abstractions;
using Notify.Broker.Abstractions;
using Notify.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Notify.Hosting;

/// <summary>
/// Hosts broker consumers that dispatch notification messages to channel providers.
/// </summary>
public sealed class NotifyDispatcherHostedService : BackgroundService
{
    private const string EmailChannelName = "email";
    private const string SmsChannelName = "sms";
    private const string PushChannelName = "push";
    private readonly IBrokerConsumer brokerConsumer;
    private readonly NotificationCodec codec;
    private readonly IProviderResolver providerResolver;
    private readonly IOptions<NotifyOptions> options;
    private readonly IHostEnvironment hostEnvironment;
    private readonly ILogger<NotifyDispatcherHostedService> logger;
    private readonly List<NotificationBatchDispatcher> dispatchers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyDispatcherHostedService"/> class.
    /// </summary>
    /// <param name="brokerConsumer">The broker consumer used to receive messages.</param>
    /// <param name="codec">The codec used to decode notification payloads.</param>
    /// <param name="providerResolver">The resolver used to locate providers by channel.</param>
    /// <param name="options">The notification options that configure dispatcher behavior.</param>
    /// <param name="hostEnvironment">The host environment used to resolve default queue prefixes.</param>
    /// <param name="logger">The logger instance for diagnostics.</param>
    public NotifyDispatcherHostedService(
        IBrokerConsumer brokerConsumer,
        NotificationCodec codec,
        IProviderResolver providerResolver,
        IOptions<NotifyOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<NotifyDispatcherHostedService> logger)
    {
        this.brokerConsumer = brokerConsumer ?? throw new ArgumentNullException(nameof(brokerConsumer));
        this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
        this.providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Stops the dispatcher and flushes any pending batches.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token used to stop the service.</param>
    /// <returns>A task that completes when the dispatcher has stopped.</returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var dispatcher in dispatchers)
        {
            await dispatcher.FlushPendingAsync(cancellationToken).ConfigureAwait(false);
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the dispatcher, starting consumers for each notification channel.
    /// </summary>
    /// <param name="stoppingToken">The token used to signal service shutdown.</param>
    /// <returns>A task that completes when all consumers have stopped.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        NotifyOptions currentOptions = options.Value;
        string queuePrefix = ResolveQueuePrefix(currentOptions);

        var tasks = new[]
        {
            StartChannelAsync(queuePrefix, NotificationChannel.Email, EmailChannelName, currentOptions.Email, stoppingToken),
            StartChannelAsync(queuePrefix, NotificationChannel.Sms, SmsChannelName, currentOptions.Sms, stoppingToken),
            StartChannelAsync(queuePrefix, NotificationChannel.Push, PushChannelName, currentOptions.Push, stoppingToken)
        };

        return Task.WhenAll(tasks);
    }

    private Task StartChannelAsync(
        string queuePrefix,
        NotificationChannel channel,
        string channelName,
        NotifyConsumerOptions consumerOptions,
        CancellationToken ct)
    {
        string destination = BrokerNaming.BuildQueueName(queuePrefix, channelName);
        BrokerConsumeOptions brokerOptions = consumerOptions.ToBrokerOptions();
        NotificationBatchDispatcher dispatcher = new(codec, providerResolver, consumerOptions, logger);
        dispatchers.Add(dispatcher);

        return brokerConsumer.ConsumeAsync(
            destination,
            (message, token) => dispatcher.HandleAsync(message, token, channel),
            brokerOptions,
            ct);
    }

    private string ResolveQueuePrefix(NotifyOptions currentOptions)
    {
        string? prefix = currentOptions.QueuePrefix;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = hostEnvironment.ApplicationName;
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new InvalidOperationException("Queue prefix must be configured to start the dispatcher.");
        }

        return prefix;
    }

    /// <summary>
    /// Aggregates notifications into batches and dispatches them to the appropriate providers.
    /// </summary>
    private sealed class NotificationBatchDispatcher
    {
        private readonly NotificationCodec codec;
        private readonly IProviderResolver providerResolver;
        private readonly ILogger logger;
        private readonly int batchSize;
        private readonly TimeSpan batchMaxWait;
        private readonly object sync = new();
        private List<NotificationPackage> buffer;
        private CancellationTokenSource? flushCts;
        private Task? flushTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationBatchDispatcher"/> class.
        /// </summary>
        /// <param name="codec">The codec used to decode broker payloads.</param>
        /// <param name="providerResolver">The resolver used to locate providers by channel.</param>
        /// <param name="consumerOptions">The consumer options that control batching.</param>
        /// <param name="logger">The logger used for batch diagnostics.</param>
        public NotificationBatchDispatcher(
            NotificationCodec codec,
            IProviderResolver providerResolver,
            NotifyConsumerOptions consumerOptions,
            ILogger logger)
        {
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
            if (consumerOptions is null)
            {
                throw new ArgumentNullException(nameof(consumerOptions));
            }

            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            batchSize = Math.Max(1, consumerOptions.BatchSize);
            batchMaxWait = consumerOptions.BatchMaxWaitMs > 0
                ? TimeSpan.FromMilliseconds(consumerOptions.BatchMaxWaitMs)
                : TimeSpan.Zero;
            buffer = new List<NotificationPackage>(batchSize);
        }

        /// <summary>
        /// Handles a broker message by decoding and dispatching the notification payload.
        /// </summary>
        /// <param name="message">The broker message containing the encoded payload.</param>
        /// <param name="ct">The cancellation token for the handler.</param>
        /// <param name="channel">The expected channel for the dispatcher.</param>
        /// <returns>A task that completes when the message is dispatched.</returns>
        public async Task HandleAsync(BrokerMessage message, CancellationToken ct, NotificationChannel channel)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            NotificationPackage package = DecodePackage(message);
            if (package.Channel != channel)
            {
                logger.LogWarning(
                    "Received a notification for channel {Channel} on the {ExpectedChannel} dispatcher.",
                    package.Channel,
                    channel);
            }

            if (!IsBatchingEnabled())
            {
                await DispatchAsync(package, ct).ConfigureAwait(false);
                return;
            }

            await EnqueueAsync(package, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Flushes any pending buffered notifications.
        /// </summary>
        /// <param name="ct">The cancellation token used to stop the flush operation.</param>
        /// <returns>A task that completes when buffered notifications are dispatched.</returns>
        public async Task FlushPendingAsync(CancellationToken ct)
        {
            List<NotificationPackage>? pending = null;
            CancellationTokenSource? pendingCts = null;

            lock (sync)
            {
                if (buffer.Count > 0)
                {
                    pending = TakeBufferedItemsLocked();
                }

                pendingCts = CancelFlushLocked();
            }

            if (pendingCts is not null)
            {
                pendingCts.Cancel();
                pendingCts.Dispose();
            }

            if (pending is not null)
            {
                await DispatchBatchAsync(pending, ct).ConfigureAwait(false);
            }
        }

        private async Task EnqueueAsync(NotificationPackage package, CancellationToken ct)
        {
            List<List<NotificationPackage>>? batches = null;
            CancellationTokenSource? pendingCts = null;

            lock (sync)
            {
                buffer.Add(package);
                if (buffer.Count >= batchSize)
                {
                    batches = new List<List<NotificationPackage>>();
                    while (buffer.Count >= batchSize)
                    {
                        List<NotificationPackage> batch = buffer.GetRange(0, batchSize);
                        buffer.RemoveRange(0, batchSize);
                        batches.Add(batch);
                    }

                    pendingCts = CancelFlushLocked();
                    if (buffer.Count > 0 && batchMaxWait > TimeSpan.Zero)
                    {
                        ScheduleFlushLocked();
                    }
                }
                else if (batchMaxWait > TimeSpan.Zero && flushTask is null)
                {
                    ScheduleFlushLocked();
                }
            }

            if (pendingCts is not null)
            {
                pendingCts.Cancel();
                pendingCts.Dispose();
            }

            if (batches is not null)
            {
                foreach (var batch in batches)
                {
                    await DispatchBatchAsync(batch, ct).ConfigureAwait(false);
                }
            }
        }

        private bool IsBatchingEnabled()
        {
            return batchSize > 1 && batchMaxWait > TimeSpan.Zero;
        }

        private NotificationPackage DecodePackage(BrokerMessage message)
        {
            NotificationPackage package = codec.Deserialize(message.Payload);
            if (string.IsNullOrWhiteSpace(package.CorrelationId) && !string.IsNullOrWhiteSpace(message.CorrelationId))
            {
                package.CorrelationId = message.CorrelationId;
            }

            return package;
        }

        private async Task DispatchAsync(NotificationPackage package, CancellationToken ct)
        {
            IProvider provider = providerResolver.Resolve(package.Channel);
            await provider.SendAsync(package, ct).ConfigureAwait(false);
        }

        private async Task DispatchBatchAsync(IReadOnlyList<NotificationPackage> packages, CancellationToken ct)
        {
            if (packages.Count == 0)
            {
                return;
            }

            NotificationChannel firstChannel = packages[0].Channel;
            bool isSingleChannel = true;
            for (int index = 1; index < packages.Count; index++)
            {
                if (packages[index].Channel != firstChannel)
                {
                    isSingleChannel = false;
                    break;
                }
            }

            if (isSingleChannel)
            {
                IProvider provider = providerResolver.Resolve(firstChannel);
                if (packages.Count == 1)
                {
                    await provider.SendAsync(packages[0], ct).ConfigureAwait(false);
                    return;
                }

                await provider.SendBatchAsync(packages, ct).ConfigureAwait(false);
                return;
            }

            Dictionary<NotificationChannel, List<NotificationPackage>> grouped = new();
            foreach (var package in packages)
            {
                if (!grouped.TryGetValue(package.Channel, out var list))
                {
                    list = new List<NotificationPackage>();
                    grouped[package.Channel] = list;
                }

                list.Add(package);
            }

            foreach (var entry in grouped)
            {
                IProvider provider = providerResolver.Resolve(entry.Key);
                List<NotificationPackage> batch = entry.Value;
                if (batch.Count == 1)
                {
                    await provider.SendAsync(batch[0], ct).ConfigureAwait(false);
                }
                else
                {
                    await provider.SendBatchAsync(batch, ct).ConfigureAwait(false);
                }
            }
        }

        private void ScheduleFlushLocked()
        {
            CancellationTokenSource newCts = new();
            flushCts = newCts;
            flushTask = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(batchMaxWait, newCts.Token).ConfigureAwait(false);
                    await FlushPendingAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        private CancellationTokenSource? CancelFlushLocked()
        {
            CancellationTokenSource? pending = flushCts;
            flushCts = null;
            flushTask = null;
            return pending;
        }

        private List<NotificationPackage> TakeBufferedItemsLocked()
        {
            List<NotificationPackage> pending = buffer;
            buffer = new List<NotificationPackage>(batchSize);
            return pending;
        }
    }
}
