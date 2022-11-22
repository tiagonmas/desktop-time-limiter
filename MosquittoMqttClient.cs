using System;
using System.Threading.Tasks;
using System.Threading;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace Wellbeing
{
    public class MqttClient
    {

    
        private MqttClient()
        {

        }

    
        private static readonly Lazy<MqttClient> lazy = new Lazy<MqttClient>(() => new MqttClient());
        public static MqttClient Instance
        {
            get
            {
                return lazy.Value;
            }
        }

        public async Task Send(String topic, String payload, bool retain)
        {

            var mqttFactory = new MqttFactory();

            using (var managedMqttClient = mqttFactory.CreateManagedMqttClient())
            {
                var mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(Properties.Settings.Default.MqttAddress)
                    .WithCredentials(Properties.Settings.Default.MqttUsername, Properties.Settings.Default.MqttPassword)
                    .Build();

                var managedMqttClientOptions = new ManagedMqttClientOptionsBuilder()
                    .WithClientOptions(mqttClientOptions)
                    .Build();

                await managedMqttClient.StartAsync(managedMqttClientOptions);

                // The application message is not sent. It is stored in an internal queue and
                // will be sent when the client is connected.
                await managedMqttClient.EnqueueAsync(topic, payload,retain:retain);

                //Console.WriteLine("The managed MQTT client is connected.");

                // Wait until the queue is fully processed.
                SpinWait.SpinUntil(() => managedMqttClient.PendingApplicationMessagesCount == 0, 10000);

                //Console.WriteLine($"Pending messages = {managedMqttClient.PendingApplicationMessagesCount}");
            }
        }
    }
}
