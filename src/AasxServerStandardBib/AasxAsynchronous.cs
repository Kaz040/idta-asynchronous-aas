using System;
using System.Security.Authentication;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AasxServerStandardBib.Mqtt;
using Extensions;
using MQTTnet;

namespace AasxAsynchronous;

public class AasxAsynchronous
{
    internal static MqttClientOptions GetClientOptions()
    {
        var settings = MqttSettings.LoadSettings();

        var useWebSocket = settings.Address?.StartsWith("ws", StringComparison.OrdinalIgnoreCase) ?? false;
        var sslProtocolSpecified = Enum.TryParse<SslProtocols>(settings.SslProtocol, ignoreCase: true, out var sslProtocol);

        var builder = new MqttClientOptionsBuilder();

        if (useWebSocket)
        {
            builder.WithWebSocketServer(o => o.WithUri($"{settings.Address}:{settings.Port}"));
        }
        else
        {
            builder.WithTcpServer(settings.Address, settings.Port);
        }

        if (settings.UseTls.HasValue && settings.UseTls.Value && sslProtocolSpecified)
        {
            builder.WithTlsOptions(o =>
            {
                // The used public broker sometimes has invalid certificates. This sample accepts all
                // certificates. This should not be used in live environments.
                o.WithCertificateValidationHandler(_ => true);
                // The default value is determined by the OS. Set manually to force version.
                o.WithSslProtocols(sslProtocol);
            });
        }

        if (!string.IsNullOrEmpty(settings.Username))
        {
            builder.WithCredentials(username: settings.Username, password: settings.Password);
        }

        if (settings.ClientId is not null)
        {
            builder.WithClientId(settings.ClientId);
        }

        return builder.Build();
    }

    private static string GetSource(IReference? reference)
    {
        if (reference is null)
        {
            return "uri:aas:shells/unknown";
        }

        var source = "";

        foreach (var item in reference.Keys)
        {
            if (item.Type.ToString() is not null and "AssetAdministrationShells")
            {
                var aasId = Convert.ToBase64String(Encoding.UTF8.GetBytes(item.Value));
                source = $"uri:aas:shells/{aasId}";
            }
            else if (item.Type.ToString() is not null and "Submodel")
            {
                var submodelId = Convert.ToBase64String(Encoding.UTF8.GetBytes(item.Value));
                source = source.Contains("uri:aas:shells") ? $"{source}/submodels/{submodelId}" : $"uri:aas:submodels/{submodelId}";
            }
            else if (item.Type.ToString() is not null and not "AssetAdministrationShells" and not "Submodel")
            {
                source = source.Contains("submodel-elements") ? $"{source}.{item.Value}" : $"{source}/submodel-elements/{item.Value}";
            }
        }

        return source;
    }

    private static string GetId(IReference? reference)
    {
        foreach (var item in reference.Keys)
        {
            if (item.Type.ToString() is not null and "AssetAdministrationShells")
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(item.Value));
            }
            else if (item.Type.ToString() is not null and "Submodel")
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(item.Value));
            }
        }

        return "unknown";
    }


    public async Task SendMessage(AasxEvents eventType, JsonObject payload, Reference? sourceInfo)
    {
        // get connection options
        var clientOptions = GetClientOptions();
        var mqttClient = new MqttClientFactory().CreateMqttClient(); //create a client object
        try
        {
            await mqttClient.ConnectAsync(clientOptions); //connect to server

            var payloadObject = new JsonObject
            {
                ["specversion"] = "1.0",
                ["id"] = Guid.NewGuid().ToString(),
                ["source"] = GetSource(sourceInfo),
                ["subject"] = GetId(sourceInfo),
                ["type"] = $"org.factory-x.events.v1.{eventType.ToString()}",
                ["datacontenttype"] = "application/json",
                ["time"] = DateTime.UtcNow.ToString(format: "yyyy-MM-dd\\THH:mm:ss\\Z"),
                ["dataschema"] = GetSchema(eventType),
                ["data"] = payload

            }.ToJsonString();

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(GetSource(sourceInfo))
                .WithPayload(payloadObject)
                .Build();
            Console.WriteLine("Publish a Message");
            await mqttClient.PublishAsync(message);
            Console.WriteLine("Publish a Message Succeed");

            await mqttClient.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    private static string GetSchema(AasxEvents eventType) => eventType switch
    {
        AasxEvents.AssetAdministrationShellAdded => "https://api.swaggerhub.com/domains/Plattform_i40/Part1-MetaModel-Schemas/V3.1.0#/components/schemas/AssetAdministrationShell",
        AasxEvents.AssetAdministrationShellChanged => "https://api.swaggerhub.com/domains/Plattform_i40/Part1-MetaModel-Schemas/V3.1.0#/components/schemas/AssetAdministrationShell",
        AasxEvents.AssetAdministrationShellDeleted => "https://api.swaggerhub.com/domains/Plattform_i40/Part1-MetaModel-Schemas/V3.1.0#/components/schemas/AssetAdministrationShell",

        AasxEvents.SubmodelAdded => "https://api.swaggerhub.com/domains/Plattform_i40/Part1-MetaModel-Schemas/V3.1.0#/components/schemas/Submodel",
        AasxEvents.SubmodelChanged => "https://api.swaggerhub.com/domains/Plattform_i40/Part1-MetaModel-Schemas/V3.1.0#/components/schemas/Submodel",
        AasxEvents.SubmodelDeleted => "https://api.swaggerhub.com/domains/Plattform_i40/Part1-MetaModel-Schemas/V3.1.0#/components/schemas/Submodel",

        AasxEvents.SubmodelElementAdded => "https://api.swaggerhub.com/domains/Plattform_i40/Part1-MetaModel-Schemas/V3.1.0#/components/schemas/Property",
        AasxEvents.SubmodelElementChanged => "https://api.swaggerhub.com/domains/Plattform_i40/Part1-MetaModel-Schemas/V3.1.0#/components/schemas/Property",
        AasxEvents.SubmodelElementDeleted => "https://api.swaggerhub.com/domains/Plattform_i40/Part1-MetaModel-Schemas/V3.1.0#/components/schemas/Property",

        AasxEvents.SubmodelElementValueChanged => "https://api.swaggerhub.com/domains/Plattform_i40/Part1-MetaModel-Schemas/V3.1.0#/components/schemas/Property",

        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null)
    };

    public async Task SendSubmodelElementUpdateAsync(ISubmodelElement submodelElement)
    {
        var submodelElementDeserialized = Jsonization.Serialize.ToJsonObject(submodelElement);

        var references = submodelElement.GetReference(); //Get submodel element reference to build into source.
        await SendMessage(AasxEvents.SubmodelElementChanged, submodelElementDeserialized, references);

    }

    public async Task SendSubmodelUpdateAsync(ISubmodel submodel)
    {
        var submodelDeserialized = Jsonization.Serialize.ToJsonObject(submodel);

        var references = submodel.GetReference(); //Get submodel reference to build into source.

        await SendMessage(AasxEvents.SubmodelChanged, submodelDeserialized, references);

    }
    public async Task SendAASUpdateAsync(IAssetAdministrationShell assetAdministrationShell)
    {
        var submodelDeserialized = Jsonization.Serialize.ToJsonObject(assetAdministrationShell);

        var references = assetAdministrationShell.GetReference(); //Get AAS reference to build into source --> This should return only AAS reference.

        await SendMessage(AasxEvents.AssetAdministrationShellChanged, submodelDeserialized, references);

    }
}
