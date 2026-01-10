using Notify.Abstractions;

namespace Notify.Core;

/// <summary>
/// Defines serialization operations for notification packages.
/// </summary>
public interface INotificationSerializer
{
    /// <summary>
    /// Serializes the provided notification package into a UTF-8 or binary payload.
    /// </summary>
    /// <param name="package">The notification package to serialize.</param>
    /// <returns>A byte array containing the serialized payload.</returns>
    byte[] Serialize(NotificationPackage package);

    /// <summary>
    /// Deserializes the provided payload into a notification package instance.
    /// </summary>
    /// <param name="payload">The serialized payload to deserialize.</param>
    /// <returns>The deserialized notification package.</returns>
    NotificationPackage Deserialize(ReadOnlySpan<byte> payload);
}
