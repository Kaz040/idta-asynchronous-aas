
using AdminShellNS;
using MQTTnet;
using AasCore.Aas3_0;
using System;
using System.Security.Authentication;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Newtonsoft.Json;
using Extensions;

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

    internal class Payload
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public Uri Source { get; set; }
        public string Data { get; set; }

        public Array path { get; set; }

    }
    public async Task SendSubmodelElementUpdateAsync(ISubmodelElement submodelElement)
    {
        var payload = Jsonization.Serialize.ToJsonObject(submodelElement);
        // get connection options
        var clientOptions = GetClientOptions();
        var mqttClient = new MqttClientFactory().CreateMqttClient(); //create a client object
        await mqttClient.ConnectAsync(clientOptions); //connect to server

        Console.WriteLine("Connected to Broker");
        //var payload = new Payload
        //{
        //    Id = submodelElement.IdShort,
        //    Type = "@ElementUpdate",
        //    Source = new Uri(submodelElement.IdShort),
        //    Data = JsonSerializer.Serialize(submodelElement)

        //};

        //string payloadString = JsonSerializer.Serialize(payload);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic("events-test")
            .WithPayload(payload.ToJsonString())
            .Build();
        Console.WriteLine("Publish a Message");
        await mqttClient.PublishAsync(message);
        Console.WriteLine("Publish a Message Succeed");

        await mqttClient.DisconnectAsync();

    }
}
