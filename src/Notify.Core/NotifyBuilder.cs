using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Notify.Abstractions;
using Notify.Broker.Abstractions;
using Notify.Broker.RabbitMQ;

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
        Services.AddSingleton<IConfigureOptions<RabbitMqOptions>>(serviceProvider =>
            new RabbitMqOptionsConfigurator(configuration, serviceProvider.GetService<IConfiguration>()));

        if (configure is not null)
        {
            Services.PostConfigure(configure);
        }

        Services.AddSingleton<RabbitMqBrokerClient>(serviceProvider =>
        {
            RabbitMqOptions brokerOptions = serviceProvider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            NotifyOptions notifyOptions = serviceProvider.GetRequiredService<IOptions<NotifyOptions>>().Value;
            IHostEnvironment environment = serviceProvider.GetRequiredService<IHostEnvironment>();
            string queuePrefix = ResolveQueuePrefix(notifyOptions, environment);
            RabbitMqBrokerClient client = new(brokerOptions, queuePrefix);
            client.EnsureStandardQueues();
            return client;
        });
        Services.AddSingleton<IBrokerPublisher>(serviceProvider => serviceProvider.GetRequiredService<RabbitMqBrokerClient>());
        Services.AddSingleton<IBrokerConsumer>(serviceProvider => serviceProvider.GetRequiredService<RabbitMqBrokerClient>());
        Services.AddSingleton<IBrokerClient>(serviceProvider => serviceProvider.GetRequiredService<RabbitMqBrokerClient>());

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

    /// <summary>
    /// Registers a provider and binds its options from the specified configuration path.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configPath">The configuration path used to bind provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
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

    /// <summary>
    /// Registers a provider and configures its options programmatically.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TOptions">The provider options type.</typeparam>
    /// <param name="configure">The configuration action for provider options.</param>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    /// <returns>The current <see cref="INotifyBuilder" /> instance for chaining.</returns>
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

    /// <summary>
    /// Registers the provider implementation for both concrete and shared provider service types.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <param name="lifetime">The service lifetime for the provider registration.</param>
    private void RegisterProvider<TProvider>(ServiceLifetime lifetime)
        where TProvider : class, IProvider
    {
        Services.Add(ServiceDescriptor.Describe(typeof(TProvider), typeof(TProvider), lifetime));
        Services.Add(ServiceDescriptor.Describe(typeof(IProvider), typeof(TProvider), lifetime));
    }

    /// <summary>
    /// Resolves the configuration section used to bind RabbitMQ options with preferred and legacy paths.
    /// </summary>
    /// <param name="notifyConfiguration">The configuration rooted at the Notify section, if available.</param>
    /// <param name="rootConfiguration">The root configuration instance used for fallback lookup.</param>
    /// <returns>The configuration section to bind, or <see langword="null" /> when none is available.</returns>
    private static IConfigurationSection? ResolveRabbitMqSection(IConfiguration? notifyConfiguration, IConfiguration? rootConfiguration)
    {
        IConfigurationSection? notifySection = notifyConfiguration?.GetSection("Broker:RabbitMq");

        if (notifySection is not null && notifySection.Exists())
        {
            return notifySection;
        }

        IConfigurationSection? preferredSection = rootConfiguration?.GetSection("Notify:Broker:RabbitMq");

        if (preferredSection is not null && preferredSection.Exists())
        {
            return preferredSection;
        }

        IConfigurationSection? legacySection = rootConfiguration?.GetSection("RabbitMq");
        return legacySection is not null && legacySection.Exists() ? legacySection : null;
    }

    /// <summary>
    /// Resolves the queue prefix from notify options or falls back to the host application name.
    /// </summary>
    /// <param name="notifyOptions">The notify options that may contain a queue prefix override.</param>
    /// <param name="environment">The host environment providing the application name fallback.</param>
    /// <returns>The resolved queue prefix used to build broker queue names.</returns>
    private static string ResolveQueuePrefix(NotifyOptions notifyOptions, IHostEnvironment environment)
    {
        string? prefix = notifyOptions.QueuePrefix;
        return string.IsNullOrWhiteSpace(prefix) ? environment.ApplicationName : prefix;
    }

    /// <summary>
    /// Configures RabbitMQ options by binding from preferred or legacy configuration sections.
    /// </summary>
    private sealed class RabbitMqOptionsConfigurator : IConfigureOptions<RabbitMqOptions>
    {
        private readonly IConfiguration? notifyConfiguration;
        private readonly IConfiguration? rootConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitMqOptionsConfigurator" /> class.
        /// </summary>
        /// <param name="notifyConfiguration">The configuration section rooted at the Notify configuration scope.</param>
        /// <param name="rootConfiguration">The root configuration used for legacy lookups.</param>
        public RabbitMqOptionsConfigurator(IConfiguration? notifyConfiguration, IConfiguration? rootConfiguration)
        {
            this.notifyConfiguration = notifyConfiguration;
            this.rootConfiguration = rootConfiguration;
        }

        /// <summary>
        /// Binds RabbitMQ options from configuration, if any matching sections are available.
        /// </summary>
        /// <param name="options">The options instance to populate.</param>
        public void Configure(RabbitMqOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            IConfigurationSection? section = ResolveRabbitMqSection(notifyConfiguration, rootConfiguration);

            if (section is not null)
            {
                section.Bind(options);
            }
        }
    }
}
