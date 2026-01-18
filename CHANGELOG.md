# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-01-18

### Added

- **Multi-channel notifications** - Email, SMS, and Push notification support
- **Broker-agnostic architecture** - Pluggable broker implementations
- **RabbitMQ broker** - Full RabbitMQ support via `Notify.Broker.RabbitMQ`
- **Serialization options** - JSON and MessagePack serialization
- **LZ4 compression** - Optimized payload sizes with LZ4
- **Batching** - Configurable batch sizes and max wait times per channel
- **Concurrency control** - Tunable prefetch and concurrency settings
- **Resilience hooks** - `INotificationRetryStrategy` and `INotificationCircuitBreaker`
- **Observability** - Metrics via `System.Diagnostics.Metrics` under the `Notify` meter
- **Pipeline support** - `INotificationPipeline` for custom middleware
- **Options hot-reload** - Configuration changes via `IOptionsMonitor`
- **Provider base class** - `ProviderBase<TOptions>` for easy provider implementation

### Packages

| Package | Description |
|---------|-------------|
| `Notify.Abstractions` | Shared contracts (`INotify`, `IProvider`, `NotificationPackage`) |
| `Notify.Core` | Producer-side publishing, serialization, and compression |
| `Notify.Hosting` | Worker-side consumption and provider dispatching |
| `Notify.Broker.Abstractions` | Broker-agnostic abstractions |
| `Notify.Broker.RabbitMQ` | RabbitMQ broker implementation |

[1.0.0]: https://github.com/atakanatali/notify/releases/tag/v1.0.0
