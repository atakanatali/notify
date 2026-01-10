using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notify.Abstractions;

namespace Notify.Core;

/// <summary>
/// Implements the notification builder used to register providers, options, and broker integrations.
/// </summary>
public sealed class NotifyBuilder : INotifyBuilder
{
    private readonly IConfiguration? configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyBuilder" /> class.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The optional configuration root for binding provider options.</param>
    public NotifyBuilder(IServiceCollection services, IConfiguration? configuration)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        this.configuration = configuration;
    }

    /// <summary>
    /// Gets the service collection being configured for the notification system.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Configures the notification system to use RabbitMQ as the underlying broker.
    /// </summary>
    /// <param name="configure">An optional configuration action for RabbitMQ options.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    public INotifyBuilder UseRabbitMq(Action<RabbitMqOptions>? configure = null)
    {
        Services.AddOptions<RabbitMqOptions>();

        if (configure is not null)
        {
            Services.Configure(configure);
        }

        return this;
    }

    /// <summary>
    /// Registers an email provider and binds its options from a configuration path.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configPath">The configuration path used to bind provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    public INotifyBuilder AddEmailProvider<TProvider, TOptions>(string configPath, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TProvider : class, IProvider
        where TOptions : class, new()
    {
        return AddProvider<TProvider, TOptions>(configPath, lifetime);
    }

    /// <summary>
    /// Registers an SMS provider and binds its options from a configuration path.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configPath">The configuration path used to bind provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    public INotifyBuilder AddSmsProvider<TProvider, TOptions>(string configPath, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TProvider : class, IProvider
        where TOptions : class, new()
    {
        return AddProvider<TProvider, TOptions>(configPath, lifetime);
    }

    /// <summary>
    /// Registers a push provider and binds its options from a configuration path.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configPath">The configuration path used to bind provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    public INotifyBuilder AddPushProvider<TProvider, TOptions>(string configPath, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TProvider : class, IProvider
        where TOptions : class, new()
    {
        return AddProvider<TProvider, TOptions>(configPath, lifetime);
    }

    /// <summary>
    /// Registers an email provider and configures its options programmatically.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configure">The configuration action for provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    public INotifyBuilder AddEmailProvider<TProvider, TOptions>(Action<TOptions> configure, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TProvider : class, IProvider
        where TOptions : class, new()
    {
        return AddProvider<TProvider, TOptions>(configure, lifetime);
    }

    /// <summary>
    /// Registers an SMS provider and configures its options programmatically.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configure">The configuration action for provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    public INotifyBuilder AddSmsProvider<TProvider, TOptions>(Action<TOptions> configure, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TProvider : class, IProvider
        where TOptions : class, new()
    {
        return AddProvider<TProvider, TOptions>(configure, lifetime);
    }

    /// <summary>
    /// Registers a push provider and configures its options programmatically.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configure">The configuration action for provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
    public INotifyBuilder AddPushProvider<TProvider, TOptions>(Action<TOptions> configure, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TProvider : class, IProvider
        where TOptions : class, new()
    {
        return AddProvider<TProvider, TOptions>(configure, lifetime);
    }

    private INotifyBuilder AddProvider<TProvider, TOptions>(string configPath, ServiceLifetime lifetime)
        where TProvider : class, IProvider
        where TOptions : class, new()
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("Configuration path must be provided.", nameof(configPath));
        }

        if (configuration is null)
        {
            throw new InvalidOperationException("Configuration is required to bind provider options from a configuration path.");
        }

        IConfigurationSection section = configuration.GetSection(configPath);

        Services.AddOptions<TOptions>().Bind(section);
        RegisterProvider<TProvider>(lifetime);

        return this;
    }

    private INotifyBuilder AddProvider<TProvider, TOptions>(Action<TOptions> configure, ServiceLifetime lifetime)
        where TProvider : class, IProvider
        where TOptions : class, new()
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        Services.AddOptions<TOptions>().Configure(configure);
        RegisterProvider<TProvider>(lifetime);

        return this;
    }

    private void RegisterProvider<TProvider>(ServiceLifetime lifetime)
        where TProvider : class, IProvider
    {
        Services.Add(ServiceDescriptor.Describe(typeof(TProvider), typeof(TProvider), lifetime));
        Services.Add(ServiceDescriptor.Describe(typeof(IProvider), typeof(TProvider), lifetime));
    }
}
