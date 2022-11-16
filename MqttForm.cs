using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Wellbeing
{
    public partial class MqttForm : Form, IDisposable
    {
        public MqttForm()
        {
            InitializeComponent();
        }


        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void MqttForm_Load(object sender, EventArgs e)
        {
            txtMqttAddress.Text = Properties.Settings.Default.MqttAddress;
            txtMqttPassword.Text = Properties.Settings.Default.MqttPassword;
            txtMqttUsername.Text = Properties.Settings.Default.MqttUsername;
            cbMqttEnabled.Checked = Properties.Settings.Default.MqttEnabled;
            txtMqttCadence.Text = Properties.Settings.Default.MqttIntervalMins.ToString();

        }


        private void btnSave_Click(object sender, EventArgs e)
        {
            
            Properties.Settings.Default.MqttAddress = txtMqttAddress.Text;
            Properties.Settings.Default.MqttPassword = txtMqttPassword.Text;
            Properties.Settings.Default.MqttUsername = txtMqttUsername.Text;
            Properties.Settings.Default.MqttEnabled = cbMqttEnabled.Checked;
            Properties.Settings.Default.MqttIntervalMins = Int32.Parse(txtMqttCadence.Text);
            Properties.Settings.Default.Save();
        }

        private void txtMqttCadence_KeyPress(object sender, KeyPressEventArgs e)
        {
            const int BACKSPACE = 8;
            const int ZERO = 48;
            const int NINE = 57;

            int keyvalue = e.KeyChar;

            if ((keyvalue == BACKSPACE) ||
            ((keyvalue >= ZERO) && (keyvalue <= NINE))) return;
            // Allow nothing else
            e.Handled = true;
        }

        private void btnRegister_Click(object sender, EventArgs e)
        {
            HomeAssistantMqtt.Instance.Register();
        }


        

        private void MqttForm_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        private void btnSend(object sender, EventArgs e)
        {
            
        }

        private void cbMqttEnabled_CheckedChanged(object sender, EventArgs e)
        {

        }
    }
}
