using System;
using System.Threading;
using System.Threading.Tasks;

namespace Notify.Abstractions;

/// <summary>
/// Defines a retry strategy for notification operations.
/// </summary>
public interface INotificationRetryStrategy
{
    /// <summary>
    /// Executes the specified send operation with retry behavior as needed.
    /// </summary>
    /// <param name="context">The context describing the notification operation.</param>
    /// <param name="sendOperation">The delegate that performs the send operation.</param>
    /// <param name="onRetry">The callback invoked when a retry attempt is scheduled.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ExecuteAsync(
        NotificationPipelineContext context,
        Func<CancellationToken, Task> sendOperation,
        Func<Exception, CancellationToken, Task> onRetry,
        CancellationToken ct);
}
