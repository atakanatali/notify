using Notify.Abstractions;
using Notify.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddNotify(builder.Configuration.GetSection("Notify"))
    .UseRabbitMq();

WebApplication app = builder.Build();

app.MapPost("/notify/email", async (INotify notify, EmailNotificationRequest request, CancellationToken ct) =>
{
    NotificationPackage package = new()
    {
        Channel = NotificationChannel.Email,
        Title = request.Title,
        Description = request.Description,
        IconUrl = request.IconUrl,
        Language = request.Language,
        CorrelationId = request.CorrelationId,
        CustomData = request.CustomData
    };

    await notify.SendAsync(package, ct);
    return Results.Accepted();
});

app.MapPost("/notify/sms", async (INotify notify, SmsNotificationRequest request, CancellationToken ct) =>
{
    List<NotificationPackage> packages = request.Recipients
        .Select(recipient => new NotificationPackage
        {
            Channel = NotificationChannel.Sms,
            Title = request.Title,
            Description = request.Message,
            CorrelationId = request.CorrelationId,
            CustomData = new Dictionary<string, string>(request.CustomData ?? new Dictionary<string, string>())
            {
                ["recipient"] = recipient
            }
        })
        .ToList();

    await notify.SendBatchAsync(packages, ct);
    return Results.Accepted();
});

app.MapPost("/notify/push", async (INotify notify, PushNotificationRequest request, CancellationToken ct) =>
{
    NotificationPackage package = new()
    {
        Channel = NotificationChannel.Push,
        Title = request.Title,
        Description = request.Body,
        IconUrl = request.IconUrl,
        Language = request.Language,
        CorrelationId = request.CorrelationId,
        CustomData = request.CustomData
    };

    await notify.SendAsync(package, ct);
    return Results.Accepted();
});

app.Run();

/// <summary>
/// Represents the payload needed to enqueue an email notification.
/// </summary>
public sealed class EmailNotificationRequest
{
    /// <summary>
    /// Gets or sets the notification title shown to recipients.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the email body or description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the URL for the icon to display in the email.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Gets or sets the language code for the email content.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier used for tracing.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets additional metadata included with the email payload.
    /// </summary>
    public Dictionary<string, string>? CustomData { get; set; }
}

/// <summary>
/// Represents the payload needed to enqueue SMS notifications.
/// </summary>
public sealed class SmsNotificationRequest
{
    /// <summary>
    /// Gets or sets the SMS title used for tracking purposes.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the SMS body content.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the target phone numbers for the batch.
    /// </summary>
    public List<string> Recipients { get; set; } = new();

    /// <summary>
    /// Gets or sets the correlation identifier used for tracing.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets additional metadata included with the SMS payload.
    /// </summary>
    public Dictionary<string, string>? CustomData { get; set; }
}

/// <summary>
/// Represents the payload needed to enqueue a push notification.
/// </summary>
public sealed class PushNotificationRequest
{
    /// <summary>
    /// Gets or sets the push notification title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the push notification body text.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Gets or sets the URL for the push notification icon.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Gets or sets the language code for the push notification.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier used for tracing.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets additional metadata included with the push payload.
    /// </summary>
    public Dictionary<string, string>? CustomData { get; set; }
}
