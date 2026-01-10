# Notify

Notify is a .NET library for **asynchronous, broker-backed notification delivery**. It decouples producers (APIs, jobs, services) from workers that perform the actual delivery (email, SMS, push), giving you:

- **Consistent payloads** across channels.
- **Broker-agnostic publishing/consumption** with pluggable providers.
- **Operational controls** for batching, concurrency, and resilience.
- **Observability hooks** via metrics and pipelines.

If you need to enqueue notifications quickly and dispatch them reliably with tuned throughput, Notify exists to fill that gap.

## Install (NuGet)

Producer (publishing notifications):

```bash
dotnet add package Notify.Core
dotnet add package Notify.Broker.RabbitMQ
```

Worker (consuming + dispatching):

```bash
dotnet add package Notify.Hosting
dotnet add package Notify.Broker.RabbitMQ
```

> If you only need shared contracts, add `Notify.Abstractions`.

## Quickstart

### Producer (API) — publish notifications

```csharp
using Microsoft.Extensions.Options;
using Notify.Abstractions;
using Notify.Broker.Abstractions;
using Notify.Broker.RabbitMQ;
using Notify.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddNotify(builder.Configuration.GetSection("Notify"))
    .UseRabbitMq();

builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddSingleton<RabbitMqBrokerClient>(serviceProvider =>
{
    RabbitMqOptions brokerOptions = serviceProvider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
    NotifyOptions notifyOptions = serviceProvider.GetRequiredService<IOptions<NotifyOptions>>().Value;
    IHostEnvironment environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    string queuePrefix = string.IsNullOrWhiteSpace(notifyOptions.QueuePrefix)
        ? environment.ApplicationName
        : notifyOptions.QueuePrefix;

    return new RabbitMqBrokerClient(brokerOptions, queuePrefix);
});

builder.Services.AddSingleton<IBrokerPublisher>(serviceProvider => serviceProvider.GetRequiredService<RabbitMqBrokerClient>());

WebApplication app = builder.Build();

app.MapPost("/notify/email", async (INotify notify, CancellationToken ct) =>
{
    NotificationPackage package = new()
    {
        Channel = NotificationChannel.Email,
        Title = "Welcome!",
        Description = "Thanks for signing up.",
        CorrelationId = Guid.NewGuid().ToString("N"),
        CustomData = new Dictionary<string, string>
        {
            ["recipient"] = "person@example.com"
        }
    };

    await notify.SendAsync(package, ct);
    return Results.Accepted();
});

app.Run();
```

### Worker — consume notifications and deliver

```csharp
using Microsoft.Extensions.Options;
using Notify.Abstractions;
using Notify.Broker.Abstractions;
using Notify.Broker.RabbitMQ;
using Notify.Core;
using Notify.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

INotifyBuilder notifyBuilder = builder.Services
    .AddNotify(builder.Configuration.GetSection("Notify"))
    .UseRabbitMq()
    .AddNotifyDispatcher();

builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddSingleton<RabbitMqBrokerClient>(serviceProvider =>
{
    RabbitMqOptions brokerOptions = serviceProvider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
    NotifyOptions notifyOptions = serviceProvider.GetRequiredService<IOptions<NotifyOptions>>().Value;
    IHostEnvironment environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    string queuePrefix = string.IsNullOrWhiteSpace(notifyOptions.QueuePrefix)
        ? environment.ApplicationName
        : notifyOptions.QueuePrefix;

    return new RabbitMqBrokerClient(brokerOptions, queuePrefix);
});

builder.Services.AddSingleton<IBrokerPublisher>(serviceProvider => serviceProvider.GetRequiredService<RabbitMqBrokerClient>());
builder.Services.AddSingleton<IBrokerConsumer>(serviceProvider => serviceProvider.GetRequiredService<RabbitMqBrokerClient>());

notifyBuilder.AddEmailProvider<SampleEmailProvider, SampleProviderOptions>("Notify:Providers:Email");

IHost app = builder.Build();
await app.RunAsync();

public sealed class SampleProviderOptions
{
    public string ProviderName { get; set; } = "SampleProvider";
    public string DefaultRecipient { get; set; } = "sample@example.com";
}

public sealed class SampleEmailProvider : ProviderBase<SampleProviderOptions>
{
    public SampleEmailProvider(ILogger<SampleEmailProvider> logger, IOptionsMonitor<SampleProviderOptions> optionsMonitor)
        : base(logger, optionsMonitor)
    {
    }

    public override NotificationChannel Channel => NotificationChannel.Email;

    public override Task SendAsync(NotificationPackage package, CancellationToken ct = default)
    {
        string recipient = package.CustomData?.GetValueOrDefault("recipient") ?? Options.DefaultRecipient;
        Logger.LogInformation("[Email] {Provider} -> {Recipient} ({Title})", Options.ProviderName, recipient, package.Title);
        return Task.CompletedTask;
    }
}
```

## appsettings.json examples

```json
{
  "Notify": {
    "QueuePrefix": "myapp",
    "Serialization": "Json",
    "Compression": {
      "Enabled": true,
      "Algorithm": "Lz4"
    },
    "Publishing": {
      "BatchSize": 50,
      "MaxInFlight": 200
    },
    "Email": {
      "Concurrency": 4,
      "Prefetch": 100,
      "BatchSize": 10,
      "BatchMaxWaitMs": 250
    },
    "Sms": {
      "Concurrency": 2,
      "Prefetch": 50,
      "BatchSize": 5,
      "BatchMaxWaitMs": 250
    },
    "Push": {
      "Concurrency": 4,
      "Prefetch": 100,
      "BatchSize": 20,
      "BatchMaxWaitMs": 250
    },
    "Providers": {
      "Email": {
        "ProviderName": "Ses",
        "DefaultRecipient": "fallback@example.com"
      }
    }
  },
  "RabbitMq": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "ExchangeName": "notify",
    "UseTls": false
  }
}
```

## Queue naming and prefixes

Notify names queues as:

```
{queuePrefix}.email
{queuePrefix}.sms
{queuePrefix}.push
```

- The **prefix** is required for producers and is read from `Notify:QueuePrefix`.
- The **worker** falls back to `IHostEnvironment.ApplicationName` if `QueuePrefix` is not set.
- Channel segments are **lowercased** (see `BrokerNaming.BuildQueueName`).

Use a stable prefix per environment (e.g., `myapp-prod`) to avoid cross-talk.

## Provider authoring guide

### Implement `IProvider`

Implement the interface directly when you need full control:

```csharp
public sealed class WebhookProvider : IProvider
{
    public NotificationChannel Channel => NotificationChannel.Push;

    public Task SendAsync(NotificationPackage package, CancellationToken ct = default)
        => SendBatchAsync([package], ct);

    public Task SendBatchAsync(IReadOnlyList<NotificationPackage> packages, CancellationToken ct = default)
    {
        // Send to external service
        return Task.CompletedTask;
    }
}
```

### Prefer `ProviderBase<TOptions>`

Use `ProviderBase<TOptions>` to get option monitoring and a logger:

```csharp
public sealed class SmsProvider : ProviderBase<SmsOptions>
{
    public SmsProvider(ILogger<SmsProvider> logger, IOptionsMonitor<SmsOptions> options)
        : base(logger, options) { }

    public override NotificationChannel Channel => NotificationChannel.Sms;

    public override Task SendAsync(NotificationPackage package, CancellationToken ct = default)
    {
        // Use Options from configuration
        return Task.CompletedTask;
    }
}
```

### Register with configuration binding

```csharp
notifyBuilder.AddSmsProvider<SmsProvider, SmsOptions>("Notify:Providers:Sms");
```

`Notify:Providers:Sms` is bound via `IOptionsMonitor<SmsOptions>`, so changes can be reloaded at runtime.

## Scaling guide

### Concurrency, prefetch, and batch tuning

- **`Notify:Publishing:BatchSize`** controls producer in-flight batching.
- **`Notify:*:Concurrency`** sets worker handler concurrency per channel.
- **`Notify:*:Prefetch`** caps how many messages are fetched from the broker per channel.
- **`Notify:*:BatchSize` + `BatchMaxWaitMs`** controls how aggressively the worker groups messages for provider dispatch.

Guidance:

- Increase **prefetch** to improve throughput for IO-bound providers.
- Increase **concurrency** to scale out processing, but ensure providers are thread-safe.
- For bulk APIs, set **batch size** > 1 to reduce per-call overhead.
- Set **BatchMaxWaitMs** to keep latency predictable at low traffic.

### RabbitMQ vs Kafka notes

- **RabbitMQ**: Prefetch is critical; set `Prefetch >= Concurrency`. Use queues per channel with a stable prefix.
- **Kafka** (via a custom `IBrokerClient`): tune consumer groups, partitions, and max poll interval. Kafka batching often happens in the broker/client, so you may keep Notify batch sizes smaller and rely on broker batching.

### Compression and serialization choices

- **Serialization**: `Json` (readable, flexible) vs `MessagePack` (smaller, faster). Set in `Notify:Serialization`.
- **Compression**: Enable LZ4 (`Notify:Compression`) when payloads are large or network-bound. Keep it off for tiny payloads to save CPU.

## Reliability

Notify provides extension points for resilience:

- **Retries** via `INotificationRetryStrategy` (inject in DI). Use exponential backoff with jitter.
- **Circuit breaker** via `INotificationCircuitBreaker` to short-circuit failing providers.
- **Dead-letter queues (DLQ)** are broker-specific: configure RabbitMQ DLX/TTL policies or Kafka topic routing for failed messages.

Pair retries with DLQ so poison messages don’t block dispatching.

## Telemetry

### Metrics

Notify emits metrics via `System.Diagnostics.Metrics` under the `Notify` meter:

- `notify_published_total`
- `notify_consumed_total`
- `notify_sent_total`
- `notify_failed_total`
- `notify_retry_total`
- `notify_send_latency_ms`

Hook this into OpenTelemetry by registering a meter provider for the `Notify` meter name.

### Tracing

There is no built-in `ActivitySource`, but you can add tracing by implementing `INotificationPipeline` and wrapping publish/dispatch operations with `Activity` or OpenTelemetry spans. Use `NotificationPipelineContext` to tag channel, operation, provider, and correlation ID.
