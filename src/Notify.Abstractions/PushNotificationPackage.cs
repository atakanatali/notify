namespace Notify.Abstractions;

/// <summary>
/// Represents a push notification payload.
/// </summary>
public sealed class PushNotificationPackage : NotificationPackage
{
    /// <summary>
    /// Gets or sets the device token for a direct push notification.
    /// </summary>
    public string? DeviceToken { get; set; }

    /// <summary>
    /// Gets or sets the topic for a broadcast push notification.
    /// </summary>
    public string? Topic { get; set; }
}
