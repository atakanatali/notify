using System.Buffers;
using MessagePack;
using MessagePack.Resolvers;
using Notify.Abstractions;

namespace Notify.Core;

/// <summary>
/// Serializes notification packages using MessagePack.
/// </summary>
public sealed class MessagePackNotificationSerializer : INotificationSerializer
{
    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithResolver(TypelessContractlessStandardResolver.Instance);

    /// <summary>
    /// Serializes the provided notification package into a MessagePack payload.
    /// </summary>
    /// <param name="package">The notification package to serialize.</param>
    /// <returns>A byte array containing the MessagePack payload.</returns>
    public byte[] Serialize(NotificationPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return MessagePackSerializer.Serialize(package, Options);
    }

    /// <summary>
    /// Deserializes the provided MessagePack payload into a notification package.
    /// </summary>
    /// <param name="payload">The MessagePack payload to deserialize.</param>
    /// <returns>The deserialized notification package.</returns>
    public NotificationPackage Deserialize(ReadOnlySpan<byte> payload)
    {
        var memory = new ReadOnlyMemory<byte>(payload.ToArray());
        var sequence = new ReadOnlySequence<byte>(memory);
        return MessagePackSerializer.Deserialize<NotificationPackage>(sequence, Options)
            ?? throw new MessagePackSerializationException("Failed to deserialize notification package.");
    }
}
