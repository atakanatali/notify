using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Notify.Abstractions;

/// <summary>
/// Provides a base implementation for notification providers with options caching.
/// </summary>
/// <typeparam name="TOptions">The options type used by the provider.</typeparam>
public abstract class ProviderBase<TOptions> : IProvider, IDisposable
    where TOptions : class, new()
{
    private readonly IDisposable? _optionsChangeSubscription;
    private TOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderBase{TOptions}"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="optionsMonitor">The options monitor used to track configuration changes.</param>
    protected ProviderBase(ILogger logger, IOptionsMonitor<TOptions> optionsMonitor)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (optionsMonitor is null)
        {
            throw new ArgumentNullException(nameof(optionsMonitor));
        }

        _options = optionsMonitor.CurrentValue;
        _optionsChangeSubscription = optionsMonitor.OnChange(updated => _options = updated);
    }

    /// <summary>
    /// Gets the logger instance for the provider.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the current options snapshot.
    /// </summary>
    protected TOptions Options => _options;

    /// <summary>
    /// Gets the notification channel supported by the provider.
    /// </summary>
    public abstract NotificationChannel Channel { get; }

    /// <summary>
    /// Sends a notification package.
    /// </summary>
    /// <param name="package">The notification payload to send.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public abstract Task SendAsync(NotificationPackage package, CancellationToken ct = default);

    /// <summary>
    /// Sends a batch of notification packages.
    /// </summary>
    /// <param name="packages">The notification payloads to send.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task SendBatchAsync(IReadOnlyList<NotificationPackage> packages, CancellationToken ct = default)
    {
        if (packages is null)
        {
            throw new ArgumentNullException(nameof(packages));
        }

        foreach (var package in packages)
        {
            if (package is null)
            {
                throw new ArgumentException("Package cannot be null.", nameof(packages));
            }

            await SendAsync(package, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Releases managed resources held by the provider.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed resources.
    /// </summary>
    /// <param name="disposing">Indicates whether the method was called by <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _optionsChangeSubscription?.Dispose();
        }
    }
}
