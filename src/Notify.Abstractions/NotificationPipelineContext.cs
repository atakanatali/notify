namespace Notify.Abstractions;

/// <summary>
/// Provides contextual data about a notification operation flowing through a pipeline.
/// </summary>
public sealed class NotificationPipelineContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationPipelineContext"/> class.
    /// </summary>
    /// <param name="package">The notification package associated with the operation.</param>
    /// <param name="operation">The logical operation name being executed.</param>
    /// <param name="packageCount">The number of packages represented by the operation.</param>
    /// <param name="destination">The broker destination, if applicable.</param>
    /// <param name="provider">The provider name, if applicable.</param>
    public NotificationPipelineContext(
        NotificationPackage package,
        string operation,
        int packageCount = 1,
        string? destination = null,
        string? provider = null)
    {
        Package = package ?? throw new ArgumentNullException(nameof(package));
        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new ArgumentException("Operation name cannot be null or whitespace.", nameof(operation));
        }

        if (packageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(packageCount), "Package count must be greater than zero.");
        }

        Operation = operation;
        PackageCount = packageCount;
        Destination = destination;
        Provider = provider;
    }

    /// <summary>
    /// Gets the notification package for the operation.
    /// </summary>
    public NotificationPackage Package { get; }

    /// <summary>
    /// Gets the channel associated with the notification operation.
    /// </summary>
    public NotificationChannel Channel => Package.Channel;

    /// <summary>
    /// Gets the logical operation name for the notification flow.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Gets the number of notification packages represented by the operation.
    /// </summary>
    public int PackageCount { get; }

    /// <summary>
    /// Gets the broker destination name, when available.
    /// </summary>
    public string? Destination { get; }

    /// <summary>
    /// Gets the provider name handling the operation, when available.
    /// </summary>
    public string? Provider { get; }
}
