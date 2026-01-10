using Microsoft.Extensions.DependencyInjection;
using Notify.Core;
using System;

namespace Notify.Hosting;

/// <summary>
/// Provides extension methods for registering the notification dispatcher host.
/// </summary>
public static class NotifyHostingExtensions
{
    /// <summary>
    /// Registers the notification dispatcher hosted service and required supporting services.
    /// </summary>
    /// <param name="builder">The notification builder used to register services.</param>
    /// <returns>The same <see cref="INotifyBuilder"/> instance for chaining.</returns>
    public static INotifyBuilder AddNotifyDispatcher(this INotifyBuilder builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        IServiceCollection services = builder.Services;
        services.AddSingleton<JsonNotificationSerializer>();
        services.AddSingleton<MessagePackNotificationSerializer>();
        services.AddSingleton<NoCompression>();
        services.AddSingleton<Lz4Compression>();
        services.AddSingleton<NotificationCodec>(serviceProvider =>
            new NotificationCodec(
                serviceProvider.GetRequiredService<JsonNotificationSerializer>(),
                serviceProvider.GetRequiredService<MessagePackNotificationSerializer>(),
                serviceProvider.GetRequiredService<NoCompression>(),
                serviceProvider.GetRequiredService<Lz4Compression>()));
        services.AddSingleton<ProviderRegistry>();
        services.AddSingleton<IProviderResolver>(serviceProvider => serviceProvider.GetRequiredService<ProviderRegistry>());
        services.AddHostedService<NotifyDispatcherHostedService>();

        return builder;
    }
}
