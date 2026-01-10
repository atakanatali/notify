using Microsoft.Extensions.Options;
using Notify.Broker.Abstractions;
using Notify.Broker.RabbitMQ;
using Notify.Core;
using Notify.Hosting;
using Notify.SampleWorker;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

INotifyBuilder notifyBuilder = builder.Services
    .AddNotify(builder.Configuration.GetSection("Notify"))
    .UseRabbitMq()
    .AddNotifyDispatcher();

builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddSingleton<RabbitMqBrokerClient>(serviceProvider =>
{
    RabbitMqOptions brokerOptions = serviceProvider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
    NotifyOptions notifyOptions = serviceProvider.GetRequiredService<IOptions<NotifyOptions>>().Value;
    IHostEnvironment environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    string queuePrefix = string.IsNullOrWhiteSpace(notifyOptions.QueuePrefix)
        ? environment.ApplicationName
        : notifyOptions.QueuePrefix;

    return new RabbitMqBrokerClient(brokerOptions, queuePrefix);
});

builder.Services.AddSingleton<IBrokerPublisher>(serviceProvider => serviceProvider.GetRequiredService<RabbitMqBrokerClient>());
builder.Services.AddSingleton<IBrokerConsumer>(serviceProvider => serviceProvider.GetRequiredService<RabbitMqBrokerClient>());
builder.Services.AddSingleton<IBrokerClient>(serviceProvider => serviceProvider.GetRequiredService<RabbitMqBrokerClient>());

notifyBuilder.AddEmailProvider<SampleEmailProvider, SampleProviderOptions>("Notify:Providers:Email");
notifyBuilder.AddSmsProvider<SampleSmsProvider, SampleProviderOptions>("Notify:Providers:Sms");
notifyBuilder.AddPushProvider<SamplePushProvider, SampleProviderOptions>("Notify:Providers:Push");

IHost app = builder.Build();
await app.RunAsync();
