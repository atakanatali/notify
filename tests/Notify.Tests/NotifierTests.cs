using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Notify.Abstractions;
using Notify.Broker.Abstractions;
using Notify.Core;
using Xunit;

namespace Notify.Tests;

public class NotifierTests
{
    [Fact]
    public async Task SendAsync_ShouldPublishToBroker()
    {
        // Arrange
        var publisher = Substitute.For<IBrokerPublisher>();
        var jsonSerializer = new JsonNotificationSerializer();
        var msgPackSerializer = new MessagePackNotificationSerializer();
        var codec = new NotificationCodec(jsonSerializer, msgPackSerializer, new NoCompression(), new Lz4Compression());
        
        var options = new NotifyOptions { QueuePrefix = "test" };
        var optionsWrapper = Options.Create(options);
        
        var notifier = new Notifier(publisher, codec, optionsWrapper, NullLogger<Notifier>.Instance, null);
        
        var package = new NotificationPackage
        {
            Channel = NotificationChannel.Email,
            Title = "Hello",
            CorrelationId = "123"
        };

        // Act
        await notifier.SendAsync(package);

        // Assert
        await publisher.Received(1).PublishAsync(
            Arg.Is<string>(s => s.Contains("email")),
            Arg.Is<BrokerMessage>(m => m.CorrelationId == "123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendBatchAsync_ShouldPublishMultipleToBroker()
    {
        // Arrange
        var publisher = Substitute.For<IBrokerPublisher>();
        var jsonSerializer = new JsonNotificationSerializer();
        var msgPackSerializer = new MessagePackNotificationSerializer();
        var codec = new NotificationCodec(jsonSerializer, msgPackSerializer, new NoCompression(), new Lz4Compression());
        
        var options = new NotifyOptions 
        { 
            QueuePrefix = "test",
            Publishing = { BatchSize = 2, MaxInFlight = 2 }
        };
        var optionsWrapper = Options.Create(options);
        
        var notifier = new Notifier(publisher, codec, optionsWrapper, NullLogger<Notifier>.Instance, null);
        
        var packages = new[]
        {
            new NotificationPackage { Channel = NotificationChannel.Email, Title = "E1" },
            new NotificationPackage { Channel = NotificationChannel.Sms, Title = "S1" }
        };

        // Act
        await notifier.SendBatchAsync(packages);

        // Assert
        await publisher.Received(2).PublishAsync(
            Arg.Any<string>(),
            Arg.Any<BrokerMessage>(),
            Arg.Any<CancellationToken>());
    }
}
