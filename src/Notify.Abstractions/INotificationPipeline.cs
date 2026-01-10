using System.Threading;
using System.Threading.Tasks;

namespace Notify.Abstractions;

/// <summary>
/// Represents a pipeline delegate used to execute a notification operation with contextual information.
/// </summary>
/// <param name="context">The context describing the notification operation.</param>
/// <param name="ct">The cancellation token for the operation.</param>
/// <returns>A task that represents the asynchronous operation.</returns>
public delegate Task NotificationPipelineDelegate(NotificationPipelineContext context, CancellationToken ct);

/// <summary>
/// Provides an extension point for wrapping notification operations with custom behavior.
/// </summary>
public interface INotificationPipeline
{
    /// <summary>
    /// Invokes the pipeline behavior for the current notification operation.
    /// </summary>
    /// <param name="context">The context describing the notification operation.</param>
    /// <param name="next">The delegate representing the next pipeline step.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous invocation.</returns>
    Task InvokeAsync(NotificationPipelineContext context, NotificationPipelineDelegate next, CancellationToken ct);
}
