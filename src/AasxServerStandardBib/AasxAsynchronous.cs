using System;
using System.Security.Authentication;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Extensions;
using MQTTnet;


namespace AasxAsynchronous;

public class AasxAsynchronous
{
    internal MqttClientOptions GetClientOptions()
    {
        //var options = new MqttClientOptionsBuilder()
        //    .WithClientId("AASXPackageXplorer MQTT Client")
        //    .WithTcpServer("localhost", 1883)
        //    .Build();

        var options = new MqttClientOptionsBuilder().WithWebSocketServer(o => o.WithUri("wss://mqttbroker.factory-x.catena-x.net:443")).WithTlsOptions(o => {
            // The used public broker sometimes has invalid certificates. This sample accepts all
            // certificates. This should not be used in live environments.
            o.WithCertificateValidationHandler(_ => true);

            // The default value is determined by the OS. Set manually to force version.
            o.WithSslProtocols(SslProtocols.Tls12);
        })
        .WithCredentials(username: "fx-subscriber", password: "password")
        .WithClientId("IDTA AASX server asynchronous function")
        .Build();

        return options;
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
                var aasId = Convert.ToBase64String(Encoding.UTF8.GetBytes((string)item.Value));
                source = $"uri:aas:shells/{aasId}";
            }
            else if (item.Type.ToString() is not null and "Submodel")
            {
                var submodelId = Convert.ToBase64String(Encoding.UTF8.GetBytes((string)item.Value));
                source = source.Contains("uri:aas:shells") ? $"{source}/submodels/{submodelId}" : $"uri:aas:submodels/{submodelId}";
            }
            else if (item.Type.ToString() is not null and not "AssetAdministrationShells" and not "Submodel")
            {
                source = source.Contains("submodel-elements") ? $"{source}.{item.Value}" : $"{source}/submodel-elements/{item.Value}";
            }
        }
        return source;
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
                ["subject"] = eventType.ToString(),
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
