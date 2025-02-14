﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wellbeing.Properties;

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
            var LockedStatus = locked ? "False" : "True";

            //CONFIG
            Register();

            //Sensor Data
            MqttClient.Instance.Send($"wellbeing/{pcName}/{userName}/LWT", "online",false);
            MqttClient.Instance.Send($"wellbeing/{pcName}/{userName}/IDLE", AwayStatus,true);
            MqttClient.Instance.Send($"wellbeing/{pcName}/{userName}/LOCKED", LockedStatus,true);
            MqttClient.Instance.Send($"wellbeing/{pcName}/{userName}/SENSOR", $"{{\"time\":{passed.Hours*60+passed.Minutes},\"passed\":\"{passed.ToString()}\",\"remaining\":\"{remaining.ToString()}\"}}",true);

        }

        /// <summary>
        /// Update Home assistant when closeing program, via mqtt
        /// </summary>

        public void Close()
        {

            //Sensor Data
            MqttClient.Instance.Send($"wellbeing/{pcName}/{userName}/IDLE", "True", true);
            

        }

        /// <summary>
        /// Update Home assistant via mqtt with total of the time spent active before goigle, aka slot
        /// </summary>
        /// <param name="total"></param>
        public void UpdateSlot(TimeSpan passedTimeSpan)
        {
            //Sensor Data
            MqttClient.Instance.Send($"wellbeing/{pcName}/{userName}/LWT", "online",false);
            MqttClient.Instance.Send($"wellbeing/{pcName}/{userName}/SLOT", $"{{\"slot\":{passedTimeSpan.Hours*60+passedTimeSpan.Minutes},\"slotdesc\":\"{passedTimeSpan.ToString()}\"}}", true);

        }
        /// <summary>
        /// Use discovery to register with Home assistant 
        /// </summary>
        public void Register()
        {
            int expire_after = (int)Settings.Default.MqttIntervalMins * 60 + 120;
            string json;
            //'Binary Sensor: Active'
            json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"name\":\"{pcName}/{userName} Active or not\", \"state_topic\": \"~IDLE\",\"payload_on\":\"True\",\"payload_off\":\"False\",\"unique_id\": \"{pcName}_{userName}_Idle\", \"device_class\":\"presence\",\"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"Windows\"}}}}";
            MqttClient.Instance.Send($"homeassistant/binary_sensor/{pcName}_{userName}_Active/config", json, true);

            ////'Binary Sensor: Locked'
            json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"name\":\"{pcName}/{userName} Locked or not\", \"state_topic\": \"~LOCKED\",\"payload_on\":\"True\",\"payload_off\":\"False\",\"unique_id\": \"{pcName}_{userName}_Locked\", \"device_class\":\"lock\",\"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"Windows\"}}}}";
            MqttClient.Instance.Send($"homeassistant/binary_sensor/{pcName}_{userName}_Locked/config", json, true);

            //'Sensor: Time'
            json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"object_id\":\"{pcName}_{userName}_Total\", \"state_class\":\"total_increasing\",\"name\":\"{pcName}/{userName} Total Time in minutes\", \"unit_of_measurement\":\"m\",\"availability_topic\": \"~LWT\", \"state_topic\": \"~SENSOR\",\"value_template\":\"{{{{value_json.time | default(none)}}}}\",\"unique_id\": \"{pcName}_{userName}_Total\", \"device_class\":\"duration\", \"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"Windows\"}}}}";
            MqttClient.Instance.Send($"homeassistant/sensor/{pcName}_{userName}_Time/config", json, true);

            //'Sensor: Time Slot'
            json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"object_id\":\"{pcName}_{userName}_Slot\", \"state_class\":\"total\",\"name\":\"{pcName}/{userName} Time of a Slot before Idle\", \"unit_of_measurement\":\"m\",\"availability_topic\": \"~LWT\", \"state_topic\": \"~SLOT\",\"value_template\":\"{{{{value_json.slot | default(none)}}}}\",\"unique_id\": \"{pcName}_{userName}_Slot\", \"device_class\":\"duration\", \"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"Windows\"}}}}";
            MqttClient.Instance.Send($"homeassistant/sensor/{pcName}_{userName}_Slot/config", json, true);

            //'Sensor: Passed Time'
            json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"object_id\":\"{pcName}_{userName}_Passed\",\"name\":\"{pcName}/{userName} Passed Time today\", \"availability_topic\": \"~LWT\", \"state_topic\": \"~SENSOR\",\"value_template\":\"{{{{value_json.passed}}}}\",\"unique_id\": \"{pcName}_{userName}_Passed\",  \"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"Windows\"}}}}";
            MqttClient.Instance.Send($"homeassistant/sensor/{pcName}_{userName}_Passed/config", json, true);

            //'Sensor: Remaining Time'
            json = $"{{\"~\":\"wellbeing/{pcName}/{userName}/\",\"object_id\":\"{pcName}_{userName}_Remaining\", \"name\":\"{pcName}/{userName} Remaining Time today\", \"availability_topic\": \"~LWT\", \"state_topic\": \"~SENSOR\",\"value_template\":\"{{{{value_json.remaining}}}}\",\"unique_id\": \"{pcName}_{userName}_Remaining\", \"dev\": {{\"identifiers\": \"{pcName}_{userName}\",\"manufacturer\": \"Wellbeing\",\"name\": \"{pcName}_{userName}\", \"model\": \"Windows\"}}}}";
            MqttClient.Instance.Send($"homeassistant/sensor/{pcName}_{userName}_Remaining/config", json, true);

        }

    }

}

