
using AasCore.Aas3_0;
using AdminShellNS;
using Extensions;
using MQTTnet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;


namespace AasxAsynchronous;
public class AasxAsynchronous
{
    internal MqttClientOptions GetClientOptions()
    {
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

    private static string GetSource(IReference reference)
    {
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

    public async Task SendMessage(JsonObject payload, Reference? sourceInfo)
    {
        // get connection options
        var clientOptions = GetClientOptions();
        var mqttClient = new MqttClientFactory().CreateMqttClient(); //create a client object
        try
        {
            await mqttClient.ConnectAsync(clientOptions); //connect to server

            var payloadObject = new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString(),
                ["source"] = GetSource(sourceInfo),
                ["type"] = "org.factory-x.events.v1.ElementUpdateEvent",
                ["datacontenttype"] = "application/json",
                ["time"] = DateTime.Now.ToString(format: "yyyy-MM-DD\\THH:mm:ss\\z"),
                ["data"] = payload

            }.ToJsonString();

            var message = new MqttApplicationMessageBuilder()
                .WithTopic("events")
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
    public async Task SendSubmodelElementUpdateAsync(ISubmodelElement submodelElement)
    {
        var submodelElementDeserialized = Jsonization.Serialize.ToJsonObject(submodelElement); 

        var references = submodelElement.GetReference(); //Get submodel element reference to build into source.

        await SendMessage(submodelElementDeserialized, references);

    }

    public async Task SendSubmodelUpdateAsync(ISubmodel submodel)
    {
        var submodelDeserialized = Jsonization.Serialize.ToJsonObject(submodel); 

        var references = submodel.GetReference(); //Get submodel reference to build into source.

        await SendMessage(submodelDeserialized, references);

    }
    public async Task SendAASUpdateAsync(IAssetAdministrationShell assetAdministrationShell)
    {
        var submodelDeserialized = Jsonization.Serialize.ToJsonObject(assetAdministrationShell); 

        var references = assetAdministrationShell.GetReference(); //Get AAS reference to build into source --> This should return only AAS reference.

        await SendMessage(submodelDeserialized, references);

    }
}
