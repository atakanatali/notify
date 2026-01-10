namespace Notify.Abstractions;

/// <summary>
/// Represents a base notification payload shared across delivery channels.
/// </summary>
public class NotificationPackage
{
    /// <summary>
    /// Gets or sets the target notification channel.
    /// </summary>
    public NotificationChannel Channel { get; set; }

    /// <summary>
    /// Gets or sets the notification title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the notification description or body.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the icon URL associated with the notification.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Gets or sets the language code for the notification content.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier for tracing.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets custom metadata for the notification.
    /// </summary>
    public Dictionary<string, string>? CustomData { get; set; }
}
