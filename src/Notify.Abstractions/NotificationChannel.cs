namespace Notify.Abstractions;

/// <summary>
/// Represents supported notification delivery channels.
/// </summary>
public enum NotificationChannel
{
    /// <summary>
    /// Email delivery channel.
    /// </summary>
    Email,
    /// <summary>
    /// SMS delivery channel.
    /// </summary>
    Sms,
    /// <summary>
    /// Push notification delivery channel.
    /// </summary>
    Push
}
