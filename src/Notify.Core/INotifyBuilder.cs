using Microsoft.Extensions.DependencyInjection;
using Notify.Abstractions;
using Notify.Broker.RabbitMQ;

namespace Notify.Core;

/// <summary>
/// Defines a fluent builder for configuring notification services, providers, and broker integrations.
/// </summary>
public interface INotifyBuilder
{
    /// <summary>
    /// Gets the service collection being configured for the notification system.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Configures the notification system to use RabbitMQ as the underlying broker.
    /// </summary>
    /// <param name="configure">An optional configuration action for RabbitMQ options.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    INotifyBuilder UseRabbitMq(Action<RabbitMqOptions>? configure = null);

    /// <summary>
    /// Registers an email provider and binds its options from a configuration path.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configPath">The configuration path used to bind provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    INotifyBuilder AddEmailProvider<TProvider, TOptions>(string configPath, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TProvider : class, IProvider
        where TOptions : class, new();

    /// <summary>
    /// Registers an SMS provider and binds its options from a configuration path.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configPath">The configuration path used to bind provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    INotifyBuilder AddSmsProvider<TProvider, TOptions>(string configPath, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TProvider : class, IProvider
        where TOptions : class, new();

    /// <summary>
    /// Registers a push provider and binds its options from a configuration path.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configPath">The configuration path used to bind provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    INotifyBuilder AddPushProvider<TProvider, TOptions>(string configPath, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TProvider : class, IProvider
        where TOptions : class, new();

    /// <summary>
    /// Registers an email provider and configures its options programmatically.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configure">The configuration action for provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    INotifyBuilder AddEmailProvider<TProvider, TOptions>(Action<TOptions> configure, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TProvider : class, IProvider
        where TOptions : class, new();

    /// <summary>
    /// Registers an SMS provider and configures its options programmatically.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configure">The configuration action for provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    INotifyBuilder AddSmsProvider<TProvider, TOptions>(Action<TOptions> configure, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TProvider : class, IProvider
        where TOptions : class, new();

    /// <summary>
    /// Registers a push provider and configures its options programmatically.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configure">The configuration action for provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    INotifyBuilder AddPushProvider<TProvider, TOptions>(Action<TOptions> configure, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TProvider : class, IProvider
        where TOptions : class, new();
}
