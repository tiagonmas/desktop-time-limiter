using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wellbeing
{
    internal class HomeAssistantMqtt
    {
        private static readonly Lazy<HomeAssistantMqtt> lazy = new Lazy<HomeAssistantMqtt>(() => new HomeAssistantMqtt());
        private static string userName;
        private static string pcName;
        public static HomeAssistantMqtt Instance
        {
            get
            {
                return lazy.Value;
            }
        }

        public HomeAssistantMqtt()
        {
            userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Replace("/","_").Replace("\\","_");
            pcName = System.Net.Dns.GetHostName().Replace("/", "_").Replace("\\", "_"); ;

        }

        /// <summary>
        /// Update Home assistant via mqtt with total time and active state or not
        /// </summary>
        /// <param name="total"></param>
        /// <param name="active"></param>
        public void Update(TimeSpan passed, TimeSpan remaining, bool idle, bool locked)
        {
            var AwayStatus = idle ? "False" : "True";
            //var LockedStatus = locked ? "False" : "True";

            //CONFIG
            string json;
            //'Binary Sensor Active'
            json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"object_id\":\"{pcName}_{userName}_Idle\",\"name\":\"{pcName}/{userName} Active or not\", \"state_topic\": \"~IDLE\",\"payload_on\":\"True\",\"payload_off\":\"False\",\"unique_id\": \"{pcName}_{userName}_Idle\", \"dev_cla\":\"presence\",\"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"Windows\"}}}}";
            MqttClient.Instance.Send($"homeassistant/binary_sensor/{pcName}_{userName}_Active/config", json);

            ////'Binary Sensor Locked'
            //json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"object_id\":\"{pcName}_{userName}_Locked\",\"name\":\"{pcName}/{userName} Locked or not\", \"stat_t\": \"~LOCKED\",\"payload_on\":\"True\",\"payload_off\":\"False\",\"unique_id\": \"{pcName}_{userName}_Locked\", \"dev_cla\":\"presence\",\"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"Windows\"}}}}";
            //MqttClient.Instance.Send($"homeassistant/binary_sensor/{pcName}_{userName}_Active/config", json);

            //'Sensor'
            json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"object_id\":\"{pcName}_{userName}_Total\", \"name\":\"{pcName}/{userName} Total Time in minutes\", \"availability_topic\": \"~LWT\", \"state_topic\": \"~SENSOR\",\"value_template\":\"{{{{value_json.time | default(none)}}}}\",\"unique_id\": \"{pcName}_{userName}_Total\", \"device_class\":\"duration\", \"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"Windows\"}}}}";
            MqttClient.Instance.Send($"homeassistant/sensor/{pcName}_{userName}_Time/config", json);

            //Sensor Data
            MqttClient.Instance.Send($"wellbeing/{pcName}/{userName}/LWT", "online");
            MqttClient.Instance.Send($"wellbeing/{pcName}/{userName}/IDLE", AwayStatus);
            //MqttClient.Instance.Send($"wellbeing/{pcName}/{userName}/LOCKED", LockedStatus);
            MqttClient.Instance.Send($"wellbeing/{pcName}/{userName}/SENSOR", $"{{\"time\":{passed.Hours*60+passed.Minutes}}}");

            Logger.Log("Mqqtt update");

        }
        /// <summary>
        /// Use discovery to register with Home assistant 
        /// </summary>
        public void Register()
        {
            string json;
            //'Binary Sensor Active'
            //json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"object_id\":\"{pcName}_{userName}_Idle\",\"name\":\"{pcName}/{userName} Active or not\", \"stat_t\": \"~IDLE\",\"payload_on\":\"True\",\"payload_off\":\"False\",\"unique_id\": \"{pcName}_{userName}_Idle\", \"dev_cla\":\"presence\",\"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"Windows\"}}}}";
            //MqttClient.Instance.Send($"homeassistant/binary_sensor/{pcName}_{userName}_Active/config", json);

            ////'Binary Sensor Locked'
            //json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"object_id\":\"{pcName}_{userName}_Locked\",\"name\":\"{pcName}/{userName} Locked or not\", \"stat_t\": \"~LOCKED\",\"payload_on\":\"True\",\"payload_off\":\"False\",\"unique_id\": \"{pcName}_{userName}_Locked\", \"dev_cla\":\"presence\",\"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"Windows\"}}}}";
            //MqttClient.Instance.Send($"homeassistant/binary_sensor/{pcName}_{userName}_Active/config", json);

            ////'Sensor'
            ////json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"object_id\":\"{pcName}_{userName}_Total\", \"name\":\"{pcName}/{userName} Total Time in minutes\", \"avty_t\": \"~STATE\", \"stat_t\": \"~SENSOR\",\"value_template\":\"{{{{value_json.time}}}}\",\"unique_id\": \"{pcName}_{userName}_Time\", \"dev_cla\":\"duration\", \"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"Windows\"}}}}";
            //json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"object_id\":\"{pcName}_{userName}_Total\", \"name\":\"{pcName}/{userName} Total Time in minutes\", \"avty_t\": \"~STATE\", \"unit_of_measurement\":\"m\",\"stat_t\": \"~SENSOR\",\"value_template\":\"{{{{value_json.time}}}}\",\"unique_id\": \"{pcName}_{userName}_Time\", \"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"Windows\"}}}}";
            //MqttClient.Instance.Send($"homeassistant/sensor/{pcName}_{userName}_Time/config", json);

            Logger.Log("Mqqtt register");

            ////'Binary Sensor'
            //string json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"object_id\":\"{pcName}_{userName}_Idle\",\"name\":\"{pcName}/{userName} Active or not\", \"avty_t\": \"~STATE\", \"pl_avail\": \"Online\",\"pl_not_avail\": \"Offline\",\"stat_t\": \"~SENSOR\",\"value_template\":\"{{{{value_json.status}}}}\",\"unique_id\": \"{pcName}_{userName}_Active\", \"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"The Best\"}}}}";
            //MqttClient.Instance.Send($"homeassistant/binary_sensor/{pcName}_{userName}_Active/config", json);

            ////'Sensor'
            //json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"object_id\":\"{pcName}_{userName}_Total\", \"name\":\"{pcName}/{userName} Total Time in minutes\", \"avty_t\": \"~STATE\", \"stat_t\": \"~SENSOR\",\"value_template\":\"{{{{value_json.time}}}}\",\"unique_id\": \"{pcName}_{userName}_Time\", \"dev_cla\":\"duration\", \"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"The Best\"}}}}";
            //MqttClient.Instance.Send($"homeassistant/sensor/{pcName}_{userName}_Time/config", json);

            //Logger.Log("Mqqtt register");

        }

    }

}

