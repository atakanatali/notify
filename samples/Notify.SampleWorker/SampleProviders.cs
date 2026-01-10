using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notify.Abstractions;

namespace Notify.SampleWorker;

/// <summary>
/// Represents configuration values used by the sample notification providers.
/// </summary>
public sealed class SampleProviderOptions
{
    /// <summary>
    /// Gets or sets the friendly name for the provider instance.
    /// </summary>
    public string ProviderName { get; set; } = "SampleProvider";

    /// <summary>
    /// Gets or sets the fallback recipient used when no explicit destination is supplied.
    /// </summary>
    public string DefaultRecipient { get; set; } = "sample@example.com";
}

/// <summary>
/// Implements a log-only email provider for the sample worker.
/// </summary>
public sealed class SampleEmailProvider : ProviderBase<SampleProviderOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SampleEmailProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger used to emit provider diagnostics.</param>
    /// <param name="optionsMonitor">The options monitor supplying provider configuration.</param>
    public SampleEmailProvider(ILogger<SampleEmailProvider> logger, IOptionsMonitor<SampleProviderOptions> optionsMonitor)
        : base(logger, optionsMonitor)
    {
    }

    /// <summary>
    /// Gets the notification channel supported by the email provider.
    /// </summary>
    public override NotificationChannel Channel => NotificationChannel.Email;

    /// <summary>
    /// Logs the email notification payload instead of delivering it.
    /// </summary>
    /// <param name="package">The notification payload to log.</param>
    /// <param name="ct">The cancellation token that can abort the operation.</param>
    /// <returns>A task that completes after the payload has been logged.</returns>
    public override Task SendAsync(NotificationPackage package, CancellationToken ct = default)
    {
        if (package is null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        string recipient = ResolveRecipient(package);
        Logger.LogInformation(
            "[Email] Provider {ProviderName} sending '{Title}' to {Recipient}.",
            Options.ProviderName,
            package.Title ?? "(no title)",
            recipient);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves the recipient value for the email notification payload.
    /// </summary>
    /// <param name="package">The notification package to inspect.</param>
    /// <returns>The resolved recipient value.</returns>
    private string ResolveRecipient(NotificationPackage package)
    {
        return package.CustomData?.GetValueOrDefault("recipient") ?? Options.DefaultRecipient;
    }
}

/// <summary>
/// Implements a log-only SMS provider for the sample worker.
/// </summary>
public sealed class SampleSmsProvider : ProviderBase<SampleProviderOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SampleSmsProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger used to emit provider diagnostics.</param>
    /// <param name="optionsMonitor">The options monitor supplying provider configuration.</param>
    public SampleSmsProvider(ILogger<SampleSmsProvider> logger, IOptionsMonitor<SampleProviderOptions> optionsMonitor)
        : base(logger, optionsMonitor)
    {
    }

    /// <summary>
    /// Gets the notification channel supported by the SMS provider.
    /// </summary>
    public override NotificationChannel Channel => NotificationChannel.Sms;

    /// <summary>
    /// Logs the SMS notification payload instead of delivering it.
    /// </summary>
    /// <param name="package">The notification payload to log.</param>
    /// <param name="ct">The cancellation token that can abort the operation.</param>
    /// <returns>A task that completes after the payload has been logged.</returns>
    public override Task SendAsync(NotificationPackage package, CancellationToken ct = default)
    {
        if (package is null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        string recipient = ResolveRecipient(package);
        Logger.LogInformation(
            "[Sms] Provider {ProviderName} sending '{Title}' to {Recipient}.",
            Options.ProviderName,
            package.Title ?? "(no title)",
            recipient);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves the recipient value for the SMS notification payload.
    /// </summary>
    /// <param name="package">The notification package to inspect.</param>
    /// <returns>The resolved recipient value.</returns>
    private string ResolveRecipient(NotificationPackage package)
    {
        return package.CustomData?.GetValueOrDefault("recipient") ?? Options.DefaultRecipient;
    }
}

/// <summary>
/// Implements a log-only push provider for the sample worker.
/// </summary>
public sealed class SamplePushProvider : ProviderBase<SampleProviderOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SamplePushProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger used to emit provider diagnostics.</param>
    /// <param name="optionsMonitor">The options monitor supplying provider configuration.</param>
    public SamplePushProvider(ILogger<SamplePushProvider> logger, IOptionsMonitor<SampleProviderOptions> optionsMonitor)
        : base(logger, optionsMonitor)
    {
    }

    /// <summary>
    /// Gets the notification channel supported by the push provider.
    /// </summary>
    public override NotificationChannel Channel => NotificationChannel.Push;

    /// <summary>
    /// Logs the push notification payload instead of delivering it.
    /// </summary>
    /// <param name="package">The notification payload to log.</param>
    /// <param name="ct">The cancellation token that can abort the operation.</param>
    /// <returns>A task that completes after the payload has been logged.</returns>
    public override Task SendAsync(NotificationPackage package, CancellationToken ct = default)
    {
        if (package is null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        string recipient = ResolveRecipient(package);
        Logger.LogInformation(
            "[Push] Provider {ProviderName} sending '{Title}' to {Recipient}.",
            Options.ProviderName,
            package.Title ?? "(no title)",
            recipient);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves the recipient value for the push notification payload.
    /// </summary>
    /// <param name="package">The notification package to inspect.</param>
    /// <returns>The resolved recipient value.</returns>
    private string ResolveRecipient(NotificationPackage package)
    {
        return package.CustomData?.GetValueOrDefault("recipient") ?? Options.DefaultRecipient;
    }
}
