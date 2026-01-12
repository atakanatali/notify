using System.Collections.Concurrent;
using System.IO;
using Notify.Broker.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Notify.Broker.RabbitMQ;

/// <summary>
/// Implements a RabbitMQ-backed broker client for publishing and consuming notification messages.
/// </summary>
public sealed class RabbitMqBrokerClient : IBrokerClient, IAsyncDisposable
{
    private const string EmailChannel = "email";
    private const string SmsChannel = "sms";
    private const string PushChannel = "push";
    private readonly RabbitMqOptions options;
    private readonly string[] standardQueues;
    private readonly bool persistentMessages;
    private readonly bool requeueOnTransientFailure;
    private readonly Func<Exception, bool> isTransientFailure;
    private readonly object connectionLock = new();
    private IConnection? connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqBrokerClient"/> class.
    /// </summary>
    /// <param name="options">The RabbitMQ connection and topology configuration.</param>
    /// <param name="queuePrefix">The prefix applied to standard queue names.</param>
    /// <param name="persistentMessages">Whether published messages should be marked as persistent.</param>
    /// <param name="requeueOnTransientFailure">Whether transient failures should requeue messages.</param>
    /// <param name="isTransientFailure">The predicate used to classify transient handler failures.</param>
    public RabbitMqBrokerClient(
        RabbitMqOptions options,
        string queuePrefix,
        bool persistentMessages = true,
        bool requeueOnTransientFailure = true,
        Func<Exception, bool>? isTransientFailure = null)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(queuePrefix))
        {
            throw new ArgumentException("Queue prefix must be provided.", nameof(queuePrefix));
        }

        standardQueues =
        [
            BrokerNaming.BuildQueueName(queuePrefix, EmailChannel),
            BrokerNaming.BuildQueueName(queuePrefix, SmsChannel),
            BrokerNaming.BuildQueueName(queuePrefix, PushChannel)
        ];
        this.persistentMessages = persistentMessages;
        this.requeueOnTransientFailure = requeueOnTransientFailure;
        this.isTransientFailure = isTransientFailure ?? IsTransientFailure;
    }

    /// <summary>
    /// Publishes a message to the specified RabbitMQ destination.
    /// </summary>
    /// <param name="destination">The queue or routing key to publish the message to.</param>
    /// <param name="message">The message payload and metadata to publish.</param>
    /// <param name="ct">The cancellation token used to abort the publish operation.</param>
    /// <returns>A task that completes when the message has been published.</returns>
    public Task PublishAsync(string destination, BrokerMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            throw new ArgumentException("Destination must be provided.", nameof(destination));
        }

        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        ct.ThrowIfCancellationRequested();

        IConnection activeConnection = GetOrCreateConnection();
        using IModel channel = activeConnection.CreateModel();
        DeclareTopology(channel, destination);

        IBasicProperties properties = channel.CreateBasicProperties();
        properties.Persistent = persistentMessages;
        properties.CorrelationId = message.CorrelationId;
        properties.MessageId = message.MessageId;
        properties.Timestamp = new AmqpTimestamp(message.CreatedUtc.ToUnixTimeSeconds());

        string exchangeName = options.ExchangeName ?? string.Empty;
        channel.BasicPublish(exchangeName, destination, properties, message.Payload);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Ensures the standard notification queues exist for email, SMS, and push channels.
    /// </summary>
    public void EnsureStandardQueues()
    {
        IConnection activeConnection = GetOrCreateConnection();
        using IModel channel = activeConnection.CreateModel();
        DeclareTopology(channel, standardQueues[0]);
    }

    /// <summary>
    /// Consumes messages from the specified RabbitMQ destination using the provided handler.
    /// </summary>
    /// <param name="destination">The queue or routing key to consume messages from.</param>
    /// <param name="handler">The handler invoked for each message.</param>
    /// <param name="options">The consumption options that control concurrency and prefetching.</param>
    /// <param name="ct">The cancellation token used to stop consuming messages.</param>
    /// <returns>A task that completes when consuming stops.</returns>
    public async Task ConsumeAsync(
        string destination,
        Func<BrokerMessage, CancellationToken, Task> handler,
        BrokerConsumeOptions options,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            throw new ArgumentException("Destination must be provided.", nameof(destination));
        }

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        IConnection activeConnection = GetOrCreateConnection();
        int concurrency = Math.Max(1, options.Concurrency);
        ushort prefetch = (ushort)Math.Max(1, options.Prefetch);
        ConcurrentBag<IModel> channels = new();
        ConcurrentBag<ConsumerRegistration> registrations = new();
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        ct.Register(() =>
        {
            foreach (ConsumerRegistration registration in registrations)
            {
                if (registration.Channel.IsOpen)
                {
                    registration.Channel.BasicCancel(registration.Tag);
                }
            }

            foreach (IModel channel in channels)
            {
                if (channel.IsOpen)
                {
                    channel.Close();
                }

                channel.Dispose();
            }

            completion.TrySetResult();
        });

        for (int index = 0; index < concurrency; index++)
        {
            IModel channel = activeConnection.CreateModel();
            channels.Add(channel);
            channel.BasicQos(0, prefetch, false);
            DeclareTopology(channel, destination);

            AsyncEventingBasicConsumer consumer = new(channel);
            consumer.Received += async (_, ea) =>
            {
                try
                {
                    BrokerMessage received = new()
                    {
                        Payload = ea.Body.ToArray(),
                        CorrelationId = ea.BasicProperties?.CorrelationId,
                        MessageId = ea.BasicProperties?.MessageId,
                        CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(ea.BasicProperties?.Timestamp.UnixTime ?? 0)
                    };

                    await handler(received, ct).ConfigureAwait(false);
                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    bool isTransient = isTransientFailure(ex);
                    bool shouldRequeue = requeueOnTransientFailure && isTransient;
                    channel.BasicNack(ea.DeliveryTag, false, shouldRequeue);
                }
            };

            string consumerTag = channel.BasicConsume(destination, false, consumer);
            registrations.Add(new ConsumerRegistration(channel, consumerTag));
        }

        await completion.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes any active RabbitMQ connection resources.
    /// </summary>
    /// <returns>A task that completes when the connection is closed.</returns>
    public ValueTask DisposeAsync()
    {
        IConnection? currentConnection;

        lock (connectionLock)
        {
            currentConnection = connection;
            connection = null;
        }

        if (currentConnection is not null)
        {
            if (currentConnection.IsOpen)
            {
                currentConnection.Close();
            }

            currentConnection.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets an active RabbitMQ connection or creates a new connection if needed.
    /// </summary>
    /// <returns>The active <see cref="IConnection" /> instance.</returns>
    private IConnection GetOrCreateConnection()
    {
        lock (connectionLock)
        {
            if (connection is null || !connection.IsOpen)
            {
                connection = BuildConnectionFactory().CreateConnection();
            }

            return connection;
        }
    }

    /// <summary>
    /// Builds a connection factory configured with the current broker options.
    /// </summary>
    /// <returns>A configured <see cref="ConnectionFactory" /> instance.</returns>
    private ConnectionFactory BuildConnectionFactory()
    {
        ConnectionFactory factory = new()
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.Username,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            DispatchConsumersAsync = true
        };

        if (options.UseTls)
        {
            factory.Ssl = new SslOption
            {
                Enabled = true,
                ServerName = options.Host
            };
        }

        return factory;
    }

    /// <summary>
    /// Declares the exchange, standard queues, and destination queue for publishing or consuming.
    /// </summary>
    /// <param name="channel">The channel used to declare topology.</param>
    /// <param name="destination">The destination queue to ensure exists.</param>
    private void DeclareTopology(IModel channel, string destination)
    {
        string? exchangeName = options.ExchangeName;

        if (!string.IsNullOrWhiteSpace(exchangeName))
        {
            channel.ExchangeDeclare(exchangeName, ExchangeType.Direct, true, false, null);
        }

        HashSet<string> queues = new(StringComparer.OrdinalIgnoreCase);
        queues.UnionWith(standardQueues);
        queues.Add(destination);

        foreach (string queue in queues)
        {
            channel.QueueDeclare(queue, true, false, false, null);

            if (!string.IsNullOrWhiteSpace(exchangeName))
            {
                channel.QueueBind(queue, exchangeName, queue);
            }
        }
    }

    /// <summary>
    /// Determines whether an exception should be treated as a transient broker failure.
    /// </summary>
    /// <param name="exception">The exception to evaluate.</param>
    /// <returns><see langword="true" /> when the exception is considered transient; otherwise <see langword="false" />.</returns>
    private static bool IsTransientFailure(Exception exception)
    {
        return exception is TimeoutException
            || exception is IOException
            || exception is BrokerUnreachableException
            || exception is AlreadyClosedException;
    }

    /// <summary>
    /// Stores the channel and consumer tag for an active subscription.
    /// </summary>
    private readonly record struct ConsumerRegistration(IModel Channel, string Tag);
}
