namespace Notify.Abstractions;

/// <summary>
/// Represents an email notification payload.
/// </summary>
public sealed class EmailNotificationPackage : NotificationPackage
{
    /// <summary>
    /// Gets or sets the recipient email address.
    /// </summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional email subject.
    /// </summary>
    public string? Subject { get; set; }
}
