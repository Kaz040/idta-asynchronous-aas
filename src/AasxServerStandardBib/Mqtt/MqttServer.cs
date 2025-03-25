using MQTTnet;
using MQTTnet.Server;
using System.Threading.Tasks;

/* For Mqtt Content:

MIT License

MQTTnet Copyright (c) 2016-2019 Christian Kratky
*/

namespace AasxServerStandardBib.Mqtt
{
    class MqttServer
    {
        MQTTnet.Server.MqttServer mqttServer;

        public MqttServer()
        {
            var mqttServerOptions = new MqttServerOptionsBuilder().WithDefaultEndpoint().Build();
            mqttServer = new MqttServerFactory().CreateMqttServer(mqttServerOptions);
        }

        public async Task MqttSeverStartAsync()
        {
            //Start a MQTT server.
            await mqttServer.StartAsync();
        }

        public async Task MqttSeverStopAsync()
        {
            await mqttServer.StopAsync();
        }
    }
}

