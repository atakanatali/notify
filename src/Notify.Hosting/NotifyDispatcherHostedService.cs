using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notify.Abstractions;
using Notify.Broker.Abstractions;
using Notify.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    private readonly IReadOnlyList<INotificationPipeline> pipelines;
    private readonly INotificationRetryStrategy? retryStrategy;
    private readonly INotificationCircuitBreaker? circuitBreaker;
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
    /// <param name="pipelines">The optional pipelines used to wrap dispatch operations.</param>
    /// <param name="retryStrategy">The optional retry strategy used for send operations.</param>
    /// <param name="circuitBreaker">The optional circuit breaker used to guard send operations.</param>
    public NotifyDispatcherHostedService(
        IBrokerConsumer brokerConsumer,
        NotificationCodec codec,
        IProviderResolver providerResolver,
        IOptions<NotifyOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<NotifyDispatcherHostedService> logger,
        IEnumerable<INotificationPipeline>? pipelines = null,
        INotificationRetryStrategy? retryStrategy = null,
        INotificationCircuitBreaker? circuitBreaker = null)
    {
        this.brokerConsumer = brokerConsumer ?? throw new ArgumentNullException(nameof(brokerConsumer));
        this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
        this.providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.pipelines = pipelines is null ? Array.Empty<INotificationPipeline>() : pipelines.ToArray();
        this.retryStrategy = retryStrategy;
        this.circuitBreaker = circuitBreaker;
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

    /// <summary>
    /// Starts consuming broker messages for the specified notification channel.
    /// </summary>
    /// <param name="queuePrefix">The queue prefix used to build the destination name.</param>
    /// <param name="channel">The channel being consumed.</param>
    /// <param name="channelName">The channel name segment used for the queue.</param>
    /// <param name="consumerOptions">The consumer options for the channel.</param>
    /// <param name="ct">The cancellation token used to stop consumption.</param>
    /// <returns>A task that completes when consumption stops.</returns>
    private Task StartChannelAsync(
        string queuePrefix,
        NotificationChannel channel,
        string channelName,
        NotifyConsumerOptions consumerOptions,
        CancellationToken ct)
    {
        string destination = BrokerNaming.BuildQueueName(queuePrefix, channelName);
        BrokerConsumeOptions brokerOptions = consumerOptions.ToBrokerOptions();
        NotificationBatchDispatcher dispatcher = new(
            codec,
            providerResolver,
            consumerOptions,
            logger,
            pipelines,
            retryStrategy,
            circuitBreaker);
        dispatchers.Add(dispatcher);

        return brokerConsumer.ConsumeAsync(
            destination,
            (message, token) => dispatcher.HandleAsync(message, token, channel),
            brokerOptions,
            ct);
    }

    /// <summary>
    /// Resolves the queue prefix from options or the host environment.
    /// </summary>
    /// <param name="currentOptions">The current notification options snapshot.</param>
    /// <returns>The resolved queue prefix.</returns>
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
        private readonly IReadOnlyList<INotificationPipeline> pipelines;
        private readonly INotificationRetryStrategy? retryStrategy;
        private readonly INotificationCircuitBreaker? circuitBreaker;
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
        /// <param name="pipelines">The optional pipelines used to wrap dispatch operations.</param>
        /// <param name="retryStrategy">The optional retry strategy used for send operations.</param>
        /// <param name="circuitBreaker">The optional circuit breaker used to guard send operations.</param>
        public NotificationBatchDispatcher(
            NotificationCodec codec,
            IProviderResolver providerResolver,
            NotifyConsumerOptions consumerOptions,
            ILogger logger,
            IReadOnlyList<INotificationPipeline> pipelines,
            INotificationRetryStrategy? retryStrategy,
            INotificationCircuitBreaker? circuitBreaker)
        {
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
            if (consumerOptions is null)
            {
                throw new ArgumentNullException(nameof(consumerOptions));
            }

            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.pipelines = pipelines ?? throw new ArgumentNullException(nameof(pipelines));
            this.retryStrategy = retryStrategy;
            this.circuitBreaker = circuitBreaker;
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
            NotifyMetrics.RecordConsumed(package.Channel);
            logger.LogDebug(
                "Consumed notification {CorrelationId} for channel {Channel}.",
                package.CorrelationId,
                package.Channel);
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

        /// <summary>
        /// Enqueues a notification package and dispatches batches when thresholds are met.
        /// </summary>
        /// <param name="package">The notification payload to enqueue.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that completes when any resulting batches are dispatched.</returns>
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

        /// <summary>
        /// Determines whether batching is enabled for the dispatcher.
        /// </summary>
        /// <returns><c>true</c> when batching is enabled; otherwise <c>false</c>.</returns>
        private bool IsBatchingEnabled()
        {
            return batchSize > 1 && batchMaxWait > TimeSpan.Zero;
        }

        /// <summary>
        /// Decodes the broker message payload into a notification package.
        /// </summary>
        /// <param name="message">The broker message containing the encoded payload.</param>
        /// <returns>The decoded notification package.</returns>
        private NotificationPackage DecodePackage(BrokerMessage message)
        {
            NotificationPackage package = codec.Deserialize(message.Payload);
            if (string.IsNullOrWhiteSpace(package.CorrelationId) && !string.IsNullOrWhiteSpace(message.CorrelationId))
            {
                package.CorrelationId = message.CorrelationId;
            }

            return package;
        }

        /// <summary>
        /// Dispatches a single notification to its channel provider.
        /// </summary>
        /// <param name="package">The notification payload to dispatch.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that completes when the provider finishes sending.</returns>
        private async Task DispatchAsync(NotificationPackage package, CancellationToken ct)
        {
            IProvider provider = providerResolver.Resolve(package.Channel);
            NotificationPipelineContext context = new(
                package,
                "Send",
                packageCount: 1,
                provider: provider.GetType().Name);
            await SendWithDiagnosticsAsync(
                context,
                provider,
                token => provider.SendAsync(package, token),
                ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Dispatches a batch of notifications to their respective channel providers.
        /// </summary>
        /// <param name="packages">The notification batch to dispatch.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that completes when the batch dispatch finishes.</returns>
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
                    NotificationPipelineContext context = new(
                        packages[0],
                        "Send",
                        packageCount: 1,
                        provider: provider.GetType().Name);
                    await SendWithDiagnosticsAsync(
                        context,
                        provider,
                        token => provider.SendAsync(packages[0], token),
                        ct).ConfigureAwait(false);
                    return;
                }

                NotificationPipelineContext context = new(
                    packages[0],
                    "SendBatch",
                    packageCount: packages.Count,
                    provider: provider.GetType().Name);
                await SendWithDiagnosticsAsync(
                    context,
                    provider,
                    token => provider.SendBatchAsync(packages, token),
                    ct).ConfigureAwait(false);
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
                    NotificationPipelineContext context = new(
                        batch[0],
                        "Send",
                        packageCount: 1,
                        provider: provider.GetType().Name);
                    await SendWithDiagnosticsAsync(
                        context,
                        provider,
                        token => provider.SendAsync(batch[0], token),
                        ct).ConfigureAwait(false);
                }
                else
                {
                    NotificationPipelineContext context = new(
                        batch[0],
                        "SendBatch",
                        packageCount: batch.Count,
                        provider: provider.GetType().Name);
                    await SendWithDiagnosticsAsync(
                        context,
                        provider,
                        token => provider.SendBatchAsync(batch, token),
                        ct).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Sends notifications through optional resilience strategies, pipelines, and diagnostics.
        /// </summary>
        /// <param name="context">The context describing the send operation.</param>
        /// <param name="provider">The provider handling the send operation.</param>
        /// <param name="sendOperation">The delegate that performs the send operation.</param>
        /// <param name="ct">The cancellation token for the operation.</param>
        /// <returns>A task that completes when the send operation finishes.</returns>
        private async Task SendWithDiagnosticsAsync(
            NotificationPipelineContext context,
            IProvider provider,
            Func<CancellationToken, Task> sendOperation,
            CancellationToken ct)
        {
            string providerName = provider.GetType().Name;
            if (circuitBreaker is not null)
            {
                bool allowSend = await circuitBreaker.AllowSendAsync(context, ct).ConfigureAwait(false);
                if (!allowSend)
                {
                    logger.LogWarning(
                        "Circuit breaker blocked notification send for provider {ProviderName}.",
                        providerName);
                    NotifyMetrics.RecordFailed(context.Channel, context.PackageCount, providerName);
                    throw new InvalidOperationException("Circuit breaker blocked the notification send operation.");
                }
            }

            Func<CancellationToken, Task> sendWithPipeline = token =>
                ExecutePipelineAsync(context, (_, pipelineToken) => sendOperation(pipelineToken), token);
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                logger.LogDebug(
                    "Sending {PackageCount} notifications to provider {ProviderName}.",
                    context.PackageCount,
                    providerName);
                if (retryStrategy is not null)
                {
                    await retryStrategy.ExecuteAsync(
                        context,
                        sendWithPipeline,
                        (exception, token) => OnRetryAsync(context, exception, providerName, token),
                        ct).ConfigureAwait(false);
                }
                else
                {
                    await sendWithPipeline(ct).ConfigureAwait(false);
                }

                stopwatch.Stop();
                NotifyMetrics.RecordSent(context.Channel, context.PackageCount, providerName);
                NotifyMetrics.RecordSendLatency(context.Channel, providerName, stopwatch.Elapsed.TotalMilliseconds);
                if (circuitBreaker is not null)
                {
                    await circuitBreaker.RecordSuccessAsync(context, ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                NotifyMetrics.RecordFailed(context.Channel, context.PackageCount, providerName);
                NotifyMetrics.RecordSendLatency(context.Channel, providerName, stopwatch.Elapsed.TotalMilliseconds);
                if (circuitBreaker is not null)
                {
                    await circuitBreaker.RecordFailureAsync(context, ex, ct).ConfigureAwait(false);
                }

                logger.LogError(
                    ex,
                    "Notification send failed for provider {ProviderName}.",
                    providerName);
                throw;
            }
        }

        /// <summary>
        /// Records retry diagnostics for a notification send operation.
        /// </summary>
        /// <param name="context">The context describing the send operation.</param>
        /// <param name="exception">The exception that triggered the retry.</param>
        /// <param name="providerName">The provider name handling the send operation.</param>
        /// <param name="ct">The cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private Task OnRetryAsync(
            NotificationPipelineContext context,
            Exception exception,
            string providerName,
            CancellationToken ct)
        {
            NotifyMetrics.RecordRetry(context.Channel, 1, providerName);
            logger.LogWarning(
                exception,
                "Retrying notification send for provider {ProviderName}.",
                providerName);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Schedules a delayed flush for the current batch buffer.
        /// </summary>
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

        /// <summary>
        /// Cancels any scheduled batch flush.
        /// </summary>
        /// <returns>The cancelled flush token source, if any.</returns>
        private CancellationTokenSource? CancelFlushLocked()
        {
            CancellationTokenSource? pending = flushCts;
            flushCts = null;
            flushTask = null;
            return pending;
        }

        /// <summary>
        /// Executes the configured pipelines in order before running the terminal operation.
        /// </summary>
        /// <param name="context">The context describing the notification operation.</param>
        /// <param name="terminal">The terminal operation to execute.</param>
        /// <param name="ct">The cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous pipeline execution.</returns>
        private Task ExecutePipelineAsync(
            NotificationPipelineContext context,
            NotificationPipelineDelegate terminal,
            CancellationToken ct)
        {
            if (pipelines.Count == 0)
            {
                return terminal(context, ct);
            }

            NotificationPipelineDelegate next = terminal;
            for (int index = pipelines.Count - 1; index >= 0; index--)
            {
                INotificationPipeline pipeline = pipelines[index];
                NotificationPipelineDelegate current = next;
                next = (pipelineContext, token) => pipeline.InvokeAsync(pipelineContext, current, token);
            }

            return next(context, ct);
        }

        /// <summary>
        /// Takes buffered notifications and resets the buffer for new items.
        /// </summary>
        /// <returns>The buffered notifications that were pending dispatch.</returns>
        private List<NotificationPackage> TakeBufferedItemsLocked()
        {
            List<NotificationPackage> pending = buffer;
            buffer = new List<NotificationPackage>(batchSize);
            return pending;
        }
    }
}
