using Notify.Core;
using Notify.Hosting;
using Notify.SampleWorker;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

INotifyBuilder notifyBuilder = builder.Services
    .AddNotify(builder.Configuration.GetSection("Notify"))
    .UseRabbitMq()
    .AddNotifyDispatcher();

notifyBuilder.AddEmailProvider<SampleEmailProvider, SampleProviderOptions>("Notify:Providers:Email");
notifyBuilder.AddSmsProvider<SampleSmsProvider, SampleProviderOptions>("Notify:Providers:Sms");
notifyBuilder.AddPushProvider<SamplePushProvider, SampleProviderOptions>("Notify:Providers:Push");

IHost app = builder.Build();
await app.RunAsync();
