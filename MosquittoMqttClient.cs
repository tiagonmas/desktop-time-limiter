using MQTTnet;
using MQTTnet.Client;
using System.Security.Authentication;
using MQTTnet.Client;
using MQTTnet.Formatter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.CompilerServices;
using MQTTnet.Extensions.ManagedClient;

namespace Wellbeing
{
    public sealed class MqttClient
    {

        private static MqttFactory mqttFactory;
        private IManagedMqttClient managedMqttClient;
        private ManagedMqttClientOptions managedMqttClientOptions;

        private MqttClient()
        {


            mqttFactory = new MqttFactory();
            managedMqttClient = mqttFactory.CreateManagedMqttClient();
            var mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer("192.168.1.65")
                    .WithCredentials("mosquitto", "1kjhhweiA.23yc")
                    .Build();

            managedMqttClientOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(mqttClientOptions)
                .Build();
        }

        private static readonly Lazy<MqttClient> lazy = new Lazy<MqttClient>(() => new MqttClient());
        public static MqttClient Instance
        {
            get
            {
                return lazy.Value;
            }
        }

        public async Task Send(String topic, String payload)
        {

            if (!managedMqttClient.IsConnected)
            { await managedMqttClient.StartAsync(managedMqttClientOptions); }

            // The application message is not sent. It is stored in an internal queue and
            // will be sent when the client is connected.
            await managedMqttClient.EnqueueAsync(topic, payload);

            Console.WriteLine("The managed MQTT client is connected.");

            // Wait until the queue is fully processed.
            SpinWait.SpinUntil(() => managedMqttClient.PendingApplicationMessagesCount == 0, 10000);

            Console.WriteLine($"Pending messages = {managedMqttClient.PendingApplicationMessagesCount}");
            
        }
    }
}
