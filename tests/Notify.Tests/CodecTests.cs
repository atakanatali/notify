using Notify.Abstractions;
using Notify.Core;
using Xunit;

namespace Notify.Tests;

public class CodecTests
{
    [Fact]
    public void Serialize_Json_NoCompression_ShouldReturnBytes()
    {
        // Arrange
        var jsonSerializer = new JsonNotificationSerializer();
        var msgPackSerializer = new MessagePackNotificationSerializer();
        var noComp = new NoCompression();
        var lz4Comp = new Lz4Compression();
        var codec = new NotificationCodec(jsonSerializer, msgPackSerializer, noComp, lz4Comp);

        var package = new NotificationPackage
        {
            Channel = NotificationChannel.Email,
            Title = "Test",
            Description = "Body"
        };

        // Act
        var result = codec.Serialize(package, NotifySerialization.Json, new NotifyCompressionOptions { Enabled = false });

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);

        var deserialized = codec.Deserialize(result);
        Assert.Equal(package.Title, deserialized.Title);
        Assert.Equal(package.Description, deserialized.Description);
    }

    [Fact]
    public void Serialize_MessagePack_Lz4_ShouldWork()
    {
        // Arrange
        var jsonSerializer = new JsonNotificationSerializer();
        var msgPackSerializer = new MessagePackNotificationSerializer();
        var noComp = new NoCompression();
        var lz4Comp = new Lz4Compression();
        var codec = new NotificationCodec(jsonSerializer, msgPackSerializer, noComp, lz4Comp);

        var package = new NotificationPackage
        {
            Channel = NotificationChannel.Sms,
            Title = "SMS Title",
            Description = "SMS Body Content"
        };

        // Act
        var result = codec.Serialize(package, NotifySerialization.MessagePack, new NotifyCompressionOptions { Enabled = true, Algorithm = NotifyCompressionAlgorithm.Lz4 });

        // Assert
        Assert.NotNull(result);
        
        var deserialized = codec.Deserialize(result);
        Assert.Equal(package.Title, deserialized.Title);
        Assert.Equal(package.Description, deserialized.Description);
    }
}
