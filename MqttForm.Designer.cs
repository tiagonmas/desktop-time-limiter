namespace Wellbeing
{
    partial class MqttForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MqttForm));
            this.cbMqttEnabled = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.txtMqttAddress = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txtMqttUsername = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.txtMqttPassword = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.txtMqttCadence = new System.Windows.Forms.TextBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnConnect = new System.Windows.Forms.Button();
            this.btnRegister = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // cbMqttEnabled
            // 
            this.cbMqttEnabled.AutoSize = true;
            this.cbMqttEnabled.Location = new System.Drawing.Point(65, 37);
            this.cbMqttEnabled.Name = "cbMqttEnabled";
            this.cbMqttEnabled.Size = new System.Drawing.Size(120, 17);
            this.cbMqttEnabled.TabIndex = 0;
            this.cbMqttEnabled.Text = "Publish data to Mqtt";
            this.cbMqttEnabled.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(62, 86);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(78, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "MQTT address";
            // 
            // txtMqttAddress
            // 
            this.txtMqttAddress.Location = new System.Drawing.Point(186, 86);
            this.txtMqttAddress.MaxLength = 32;
            this.txtMqttAddress.Name = "txtMqttAddress";
            this.txtMqttAddress.Size = new System.Drawing.Size(154, 20);
            this.txtMqttAddress.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(62, 135);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(87, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "MQTT username";
            // 
            // txtMqttUsername
            // 
            this.txtMqttUsername.Location = new System.Drawing.Point(186, 135);
            this.txtMqttUsername.MaxLength = 32;
            this.txtMqttUsername.Name = "txtMqttUsername";
            this.txtMqttUsername.Size = new System.Drawing.Size(154, 20);
            this.txtMqttUsername.TabIndex = 4;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(62, 180);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(86, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "MQTT password";
            // 
            // txtMqttPassword
            // 
            this.txtMqttPassword.Location = new System.Drawing.Point(186, 180);
            this.txtMqttPassword.MaxLength = 32;
            this.txtMqttPassword.Name = "txtMqttPassword";
            this.txtMqttPassword.Size = new System.Drawing.Size(154, 20);
            this.txtMqttPassword.TabIndex = 6;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(62, 228);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(118, 13);
            this.label4.TabIndex = 7;
            this.label4.Text = "MQTT cadence in mins";
            // 
            // txtMqttCadence
            // 
            this.txtMqttCadence.Location = new System.Drawing.Point(186, 225);
            this.txtMqttCadence.MaxLength = 10;
            this.txtMqttCadence.Name = "txtMqttCadence";
            this.txtMqttCadence.Size = new System.Drawing.Size(154, 20);
            this.txtMqttCadence.TabIndex = 8;
            this.txtMqttCadence.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtMqttCadence_KeyPress);
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(64, 260);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(121, 26);
            this.btnSave.TabIndex = 9;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(219, 260);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(121, 26);
            this.btnCancel.TabIndex = 10;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(65, 305);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(115, 22);
            this.btnConnect.TabIndex = 12;
            this.btnConnect.Text = "Send";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnSend);
            // 
            // btnRegister
            // 
            this.btnRegister.Location = new System.Drawing.Point(219, 305);
            this.btnRegister.Name = "btnRegister";
            this.btnRegister.Size = new System.Drawing.Size(115, 22);
            this.btnRegister.TabIndex = 13;
            this.btnRegister.Text = "Register";
            this.btnRegister.UseVisualStyleBackColor = true;
            this.btnRegister.Click += new System.EventHandler(this.btnRegister_Click);
            // 
            // MqttForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(415, 363);
            this.Controls.Add(this.btnRegister);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.txtMqttCadence);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.txtMqttPassword);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.txtMqttUsername);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.txtMqttAddress);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cbMqttEnabled);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MqttForm";
            this.Text = "MqttForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MqttForm_FormClosing);
            this.Load += new System.EventHandler(this.MqttForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox cbMqttEnabled;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtMqttAddress;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtMqttUsername;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtMqttPassword;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtMqttCadence;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnRegister;
    }
}