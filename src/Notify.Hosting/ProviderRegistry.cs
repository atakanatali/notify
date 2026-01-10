using System;
using System.Collections.Generic;
using System.Linq;
using Notify.Abstractions;

namespace Notify.Hosting;

/// <summary>
/// Defines a resolver for locating notification providers by channel.
/// </summary>
public interface IProviderResolver
{
    /// <summary>
    /// Resolves the provider responsible for the specified notification channel.
    /// </summary>
    /// <param name="channel">The notification channel to resolve.</param>
    /// <returns>The provider implementation for the requested channel.</returns>
    IProvider Resolve(NotificationChannel channel);
}

/// <summary>
/// Maintains a validated registry of notification providers keyed by channel.
/// </summary>
public sealed class ProviderRegistry : IProviderResolver
{
    private readonly IReadOnlyDictionary<NotificationChannel, IProvider> providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderRegistry"/> class.
    /// </summary>
    /// <param name="providers">The registered notification providers.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provider collection is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a channel has zero or multiple providers.</exception>
    public ProviderRegistry(IEnumerable<IProvider> providers)
    {
        if (providers is null)
        {
            throw new ArgumentNullException(nameof(providers));
        }

        var providerList = providers.ToList();
        var grouped = providerList
            .GroupBy(provider => provider.Channel)
            .ToDictionary(group => group.Key, group => group.ToList());

        Dictionary<NotificationChannel, IProvider> resolved = new();
        foreach (var channel in Enum.GetValues<NotificationChannel>())
        {
            if (!grouped.TryGetValue(channel, out var channelProviders) || channelProviders.Count == 0)
            {
                throw new InvalidOperationException($"No provider registered for channel '{channel}'.");
            }

            if (channelProviders.Count > 1)
            {
                throw new InvalidOperationException($"Multiple providers registered for channel '{channel}'.");
            }

            resolved[channel] = channelProviders[0];
        }

        this.providers = resolved;
    }

    /// <summary>
    /// Resolves the provider responsible for the specified notification channel.
    /// </summary>
    /// <param name="channel">The notification channel to resolve.</param>
    /// <returns>The provider implementation for the requested channel.</returns>
    public IProvider Resolve(NotificationChannel channel)
    {
        if (!providers.TryGetValue(channel, out var provider))
        {
            throw new InvalidOperationException($"No provider registered for channel '{channel}'.");
        }

        return provider;
    }
}
