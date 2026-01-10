namespace Notify.Abstractions;

/// <summary>
/// Defines a service for sending notifications.
/// </summary>
public interface INotify
{
    /// <summary>
    /// Sends a notification package.
    /// </summary>
    /// <param name="package">The notification payload to send.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SendAsync(NotificationPackage package, CancellationToken ct = default);

    /// <summary>
    /// Sends a batch of notification packages.
    /// </summary>
    /// <param name="packages">The notification payloads to send.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SendBatchAsync(IReadOnlyList<NotificationPackage> packages, CancellationToken ct = default);
}
