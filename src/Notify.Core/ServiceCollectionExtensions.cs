using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Notify.Core;

/// <summary>
/// Provides extension methods for registering notification services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds notification services with default options and returns a builder for further configuration.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>A configured <see cref="INotifyBuilder" /> instance.</returns>
    public static INotifyBuilder AddNotify(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddOptions<NotifyOptions>();

        return new NotifyBuilder(services, configuration: null);
    }

    /// <summary>
    /// Adds notification services and configures options programmatically.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">The configuration action for <see cref="NotifyOptions" />.</param>
    /// <returns>A configured <see cref="INotifyBuilder" /> instance.</returns>
    public static INotifyBuilder AddNotify(this IServiceCollection services, Action<NotifyOptions> configure)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        services.AddOptions<NotifyOptions>().Configure(configure);

        return new NotifyBuilder(services, configuration: null);
    }

    /// <summary>
    /// Adds notification services and binds options from a configuration section.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="section">The configuration section used to bind <see cref="NotifyOptions" />.</param>
    /// <returns>A configured <see cref="INotifyBuilder" /> instance.</returns>
    public static INotifyBuilder AddNotify(this IServiceCollection services, IConfigurationSection section)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (section is null)
        {
            throw new ArgumentNullException(nameof(section));
        }

        services.AddOptions<NotifyOptions>().Bind(section);

        return new NotifyBuilder(services, section);
    }
}
