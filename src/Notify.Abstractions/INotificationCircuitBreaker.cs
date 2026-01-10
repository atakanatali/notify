using System;
using System.Threading;
using System.Threading.Tasks;

namespace Notify.Abstractions;

/// <summary>
/// Defines a circuit breaker strategy for notification operations.
/// </summary>
public interface INotificationCircuitBreaker
{
    /// <summary>
    /// Determines whether a notification send operation is allowed.
    /// </summary>
    /// <param name="context">The context describing the notification operation.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that resolves to <c>true</c> when sending is allowed.</returns>
    Task<bool> AllowSendAsync(NotificationPipelineContext context, CancellationToken ct);

    /// <summary>
    /// Records a successful notification send operation.
    /// </summary>
    /// <param name="context">The context describing the notification operation.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RecordSuccessAsync(NotificationPipelineContext context, CancellationToken ct);

    /// <summary>
    /// Records a failed notification send operation.
    /// </summary>
    /// <param name="context">The context describing the notification operation.</param>
    /// <param name="exception">The exception thrown by the operation.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RecordFailureAsync(NotificationPipelineContext context, Exception exception, CancellationToken ct);
}
