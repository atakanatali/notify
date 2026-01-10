using System.Text.Json;
using Notify.Abstractions;

namespace Notify.Core;

/// <summary>
/// Serializes notification packages using System.Text.Json.
/// </summary>
public sealed class JsonNotificationSerializer : INotificationSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serializes the provided notification package into a UTF-8 JSON payload.
    /// </summary>
    /// <param name="package">The notification package to serialize.</param>
    /// <returns>A byte array containing the JSON payload.</returns>
    public byte[] Serialize(NotificationPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return JsonSerializer.SerializeToUtf8Bytes(package, Options);
    }

    /// <summary>
    /// Deserializes the provided UTF-8 JSON payload into a notification package.
    /// </summary>
    /// <param name="payload">The JSON payload to deserialize.</param>
    /// <returns>The deserialized notification package.</returns>
    public NotificationPackage Deserialize(ReadOnlySpan<byte> payload)
    {
        return JsonSerializer.Deserialize<NotificationPackage>(payload, Options)
            ?? throw new JsonException("Failed to deserialize notification package.");
    }
}
