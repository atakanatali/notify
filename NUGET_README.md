# Notify

**Asynchronous, broker-backed notification delivery for .NET**

---

## Quickstart

```csharp
// Producer - publish notifications
builder.Services
    .AddNotify(builder.Configuration.GetSection("Notify"))
    .UseRabbitMq();

await notify.SendAsync(new NotificationPackage
{
    Channel = NotificationChannel.Email,
    Title = "Welcome!",
    Description = "Thanks for signing up.",
    CustomData = new() { ["recipient"] = "user@example.com" }
});
```

```csharp
// Worker - consume and deliver
builder.Services
    .AddNotify(builder.Configuration.GetSection("Notify"))
    .UseRabbitMq()
    .AddNotifyDispatcher()
    .AddEmailProvider<MyEmailProvider, MyEmailOptions>("Notify:Providers:Email");
```

## Features

- **Broker-Agnostic** - Pluggable message broker support (RabbitMQ included)
- **Multi-Channel** - Email, SMS, Push with consistent payload structure
- **Batching & Concurrency** - Tunable throughput controls per channel
- **Compression** - LZ4/GZip support for large payloads
- **Serialization** - JSON or MessagePack
- **Observability** - Metrics via System.Diagnostics.Metrics

## Package Structure

| Package | Description |
|---------|-------------|
| `Notify.Abstractions` | Interfaces and core models |
| `Notify.Core` | Producer-side publishing, serialization |
| `Notify.Hosting` | Worker-side consumption and dispatching |
| `Notify.Broker.RabbitMQ` | RabbitMQ broker implementation |

## Documentation

For full documentation, visit [GitHub](https://github.com/atakanatali/notify).
