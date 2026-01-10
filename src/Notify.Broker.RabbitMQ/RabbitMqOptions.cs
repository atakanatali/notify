namespace Notify.Broker.RabbitMQ;

/// <summary>
/// Represents the connection and topology options required to communicate with a RabbitMQ broker.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>
    /// Gets or sets the RabbitMQ host name or IP address to connect to.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the TCP port used for the RabbitMQ connection.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Gets or sets the username used to authenticate with the RabbitMQ broker.
    /// </summary>
    public string Username { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the password used to authenticate with the RabbitMQ broker.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the virtual host name to scope broker resources within RabbitMQ.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Gets or sets the optional exchange name used for publishing and binding queues.
    /// </summary>
    public string? ExchangeName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether TLS should be enabled for the broker connection.
    /// </summary>
    public bool UseTls { get; set; }
}
