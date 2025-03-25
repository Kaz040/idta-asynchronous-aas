using System;
using Microsoft.Extensions.Configuration;

namespace AasxServerStandardBib.Mqtt;

public sealed class MqttSettings
{
    private const string SectionKey = "MQTT";

    public string? Address { get; set; }
    public int? Port { get; set; }
    public bool? UseTls { get; set; }
    public string? SslProtocol { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ClientId { get; set; }

    public static MqttSettings LoadSettings(IConfiguration? config = null)
    {
        // Load configuration if not provided
        config ??= LoadConfiguration();

        // Load settings
        var settings = config.GetRequiredSection(SectionKey).Get<MqttSettings>()
            ?? throw new ApplicationException("Could not load app settings for MQTT.");

        if (string.IsNullOrWhiteSpace(settings.Address) || !settings.Port.HasValue)
        {
            throw new ApplicationException("MQTT broker address and port are required.");
        }

        if (settings.UseTls == true && string.IsNullOrWhiteSpace(settings.SslProtocol))
        {
            throw new ApplicationException("MQTT SSL protocol is required when using TLS.");
        }

        if ((string.IsNullOrWhiteSpace(settings.Username) && !string.IsNullOrWhiteSpace(settings.Password))
            || (!string.IsNullOrWhiteSpace(settings.Username) && string.IsNullOrWhiteSpace(settings.Password)))
        {
            throw new ApplicationException("MQTT username and password must be both set or both empty.");
        }

        return settings;
    }

    private static IConfiguration LoadConfiguration() => new ConfigurationBuilder()
            // appsettings.json or environment vars are required
            .AddJsonFile("appsettings.json", optional: true) // 
            // appsettings.Development.json" is optional, values override appsettings.json
            .AddJsonFile($"appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
}
