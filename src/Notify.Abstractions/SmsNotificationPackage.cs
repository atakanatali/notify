namespace Notify.Abstractions;

/// <summary>
/// Represents an SMS notification payload.
/// </summary>
public sealed class SmsNotificationPackage : NotificationPackage
{
    /// <summary>
    /// Gets or sets the recipient phone number.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional sender identifier.
    /// </summary>
    public string? SenderId { get; set; }
}
