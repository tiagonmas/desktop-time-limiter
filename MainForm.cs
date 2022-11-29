using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Reflection;
using System.Windows.Forms;
using System.Globalization;
using System.Resources;
using System.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace Wellbeing
{
    public partial class MainForm : Form
    {
        private const byte DefaultResetHour = 3;
        private const byte DefaultMaxTimeMins = 240;
        private const byte DefaultIdleThresholdMins = 6;
        private const string DateTimeFormatter = "G";
        
        private readonly ResetChecker ResetChecker;
        private readonly UpdateChecker UpdateChecker;
        internal readonly PcLocker PcLocker;
        private int LastShownMins = int.MaxValue;
        private string? Password;
        private Button btnMqtt;
        private StatusStrip statusStrip1;
        private Label lblSlot;
        private Label label1;
        private Label lblCurrentSlot;
        private ProgressBar pbIdle;
        private Label label2;
        private ToolStripProgressBar toolStripProgressBar1;
        private ToolStripStatusLabel toolStripStatusLabel1;
        private ToolStripStatusLabel versionLbl;
        private ToolStripSplitButton toolStripSplitButton1;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem restartToolStripMenuItem;
        private ToolStripMenuItem settingsToolStripMenuItem;
        private ToolStripMenuItem changeElapsedTimeToolStripMenuItem;
        private ToolStripMenuItem changeTimeOfDayToResetToolStripMenuItem;
        private ToolStripMenuItem changeMaxAllowedTimeToolStripMenuItem;
        private ToolStripMenuItem changePasswordToolStripMenuItem;
        private ToolStripMenuItem changeIdleTimeNeededToStopToolStripMenuItem;
        private ToolStripMenuItem pauseStartToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private static readonly List<(int timePointMins, Action action)> TimeEvents = new()
        {
            (30, () =>
            {
                PlayNotification(30);
                //MessageBox.Show("Zbývá 30 minut", "Oznámení o zbývajícím čase", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }),
            (10, () =>
            {
                PlayNotification(10);
                //MessageBox.Show("Zbývá 10 minut", "Oznámení o zbývajícím čase", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            })
        };

        public MainForm()
        {

            Thread.CurrentThread.CurrentUICulture = CultureInfo.InstalledUICulture;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            resources.ApplyResources(this, "$this");

            InitializeComponent();

            versionLbl.Text = Program.Version;

            DateTime lastOpen = Config.GetDateTime(Config.Property.LastOpenOrResetDateTime, DateTimeFormatter) ?? DateTime.MinValue;
            int resetHour = Config.GetIntOrNull(Config.Property.ResetHour) ?? DefaultResetHour;
            ResetChecker = new(resetHour, lastOpen);
            
            Password = Config.GetValueOrNull(Config.Property.Password) ?? Properties.Settings.Default.DefaultPassword;
            UpdateChecker = new();
            PcLocker = new(this);

            ResetChecker.ShouldResetHandler += (_, _) => Reset();
            UpdateChecker.OnUpdateAvailable += (_, _) =>
            {
                Updater.DownloadLatestUpdateAsync(UpdateHandler);
            };
            
           
            PassedTimeWatcher.OnRunningChanged += (_, running) =>
                Invoke(new EventHandler((_, _) =>
                    { StatusLbl.Text = running ? Properties.Resources.On : Properties.Resources.Suspended;
                        Logger.Log("OnRunningChanged: " + running);
                    }
                ));
            PassedTimeWatcher.OnEnterIdle += PassedTimeWatcher_OnEnterIdle;
            PassedTimeWatcher.OnUpdate += (_, time) =>
                Invoke(new EventHandler((_, _) => HandleTick(time.passedMillis, time.remainingMillis, time.CurrentSlotDuration)));
            PassedTimeWatcher.OnMaxTimeReached += (_, _) =>
                Invoke(new EventHandler((_, _) => HandleMaxTimeReached()));

            int passedSecsToday = Config.GetIntOrNull(Config.Property.PassedTodaySecs) ?? 0;
            PassedTimeWatcher.PassedMillis = (int)TimeSpan.FromSeconds(passedSecsToday).TotalMilliseconds;
            PassedTimeWatcher.MaxTime = TimeSpan.FromMinutes(Config.GetIntOrNull(Config.Property.MaxTimeMins) ?? DefaultMaxTimeMins);
            PassedTimeWatcher.IdleThreshold = TimeSpan.FromMinutes(Config.GetIntOrNull(Config.Property.IdleThresholdMins) ?? DefaultIdleThresholdMins);
            
            Config.SetValue(Config.Property.LastOpenOrResetDateTime, DateTime.Now.ToString(DateTimeFormatter));
        }

        private void PassedTimeWatcher_OnEnterIdle(object sender, TimeSpan slotDuration)
        {
                Invoke(new Action(() =>
                {
                    lblSlot.Text = "Last Slot:: " + Format(slotDuration);
                    lblCurrentSlot.Text = "Current Slot: 0";
                }));
        }


        private static void UpdateHandler(Action update)
        {
            PassedTimeWatcher.SaveToConfig();
            update();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            Logger.Log("OnHandleCreated Running becomes true");
            if (ResetChecker.ShouldResetPassedTime())
            {
                Logger.Log("OnHandleCreated Reset Timer");
                Reset();
            }
              

            /*if (PassedTimeWatcher.PassedMillis >= PassedTimeWatcher.MaxTime.TotalMilliseconds)
            {
                MessageBox.Show("Dnes už nezbývá žádný čas", "Oznámení o zbývajícím čase", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }*/
            
            ResetChecker.Start();
            UpdateChecker.Start();
            PassedTimeWatcher.Running = true;
            base.OnHandleCreated(e);
        }

        // To hide the window from task manager. It is still visible in processes.
        protected override CreateParams CreateParams {
            get {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80;  // Turn on WS_EX_TOOLWINDOW
                return cp;
            }
        }

        private void Reset()
        {
            Logger.Log("Resetting passed time.",true);
            PassedTimeWatcher.PassedMillis = 0;
            UnlockIfLocked();
            Config.SetValue(Config.Property.PassedTodaySecs, "0");
            Config.SetValue(Config.Property.LastOpenOrResetDateTime, DateTime.Now.AddMinutes(1).ToString(DateTimeFormatter));
        }

        private void UnlockIfLocked()
        {
            if (!PcLocker.Locked)
                return;
            
            PcLocker.Unlock();
            PassedTimeWatcher.Running = true;
        }

        private void HandleMaxTimeReached()
        {
            PlayNotification(0);
            Opacity = 1;
            PcLocker.Lock();
        }

        private void HandleTick(int passedMillis, int remainingMillis, TimeSpan currentSlotDuration)
        {
            if (Opacity != 0)
            { 
                IdleLbl.Text = Utils.FormatTime(IdleTimeWatcher.IdleTimeMillis);
                
                if ((int)TimeSpan.FromMilliseconds(IdleTimeWatcher.IdleTimeMillis).TotalSeconds<=pbIdle.Maximum)
                {
                    pbIdle.Value = (int)TimeSpan.FromMilliseconds(IdleTimeWatcher.IdleTimeMillis).TotalSeconds;
                }
                
            }
            TimeSpan passedTime = TimeSpan.FromMilliseconds(passedMillis);
            TimeSpan remainingTime = TimeSpan.FromMilliseconds(remainingMillis);
            string formatted = Properties.Resources.Time+": " + Format(passedTime) + " / " + Format(PassedTimeWatcher.MaxTime);
            TimeLbl.Text = formatted;
            lblCurrentSlot.Text = "Current Slot: " + Format(currentSlotDuration);
            
            int remainingTimeMins = (int)remainingTime.TotalMinutes;
            if (LastShownMins < remainingTimeMins)
                LastShownMins = int.MaxValue;
            
            foreach ((int timePointMins, Action action) in TimeEvents)
            {
                if (LastShownMins <= timePointMins || (int)remainingTime.TotalMinutes != timePointMins)
                    continue;

                LastShownMins = timePointMins;
                action.Invoke();
            }
        }

        // Make sure the window doesn't go out of screen's bounds.
        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            //int rightSideXPos = Location.X + Width;
            //int bottomSideYPos = Location.Y + Height;
            
            //int screenMaxY = Screen.PrimaryScreen.Bounds.Height;
            //int screenMaxX = Screen.PrimaryScreen.Bounds.Width;

            //if (rightSideXPos > screenMaxX)
            //    Location = Location with { X = screenMaxX - Width };
            //if (bottomSideYPos > screenMaxY)
            //    Location = Location with { Y = screenMaxY - Height };
            //if (Location.X < 0)
            //    Location = Location with { X = 0 };
            //if (Location.Y < 0)
            //    Location = Location with { Y = 0 };
        }


        private bool RequestPassword() => ObtainTextOrNull(Properties.Resources.Password, true) == Password;
        private static string? ObtainTextOrNull(string title, bool password)
        {
            var dialog = new TextDialog();
            dialog.Text = title;
            if (password)
                dialog.TextBox.UseSystemPasswordChar = true;
            
            return dialog.ShowDialog() == DialogResult.OK ? dialog.TextBox.Text : null;
        }
        
        private static TimeSpan? ObtainTimeOrNull(TimeSpan? initialTime)
        {
            using var dialog = new TimeDialog();
            if (initialTime != null)
            {
                dialog.HoursBox.Value = (int)initialTime.Value.TotalHours;
                dialog.MinutesBox.Value = initialTime.Value.Minutes;   
            }

            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
                return TimeSpan.FromMinutes((int)dialog.HoursBox.Value * 60 + (int)dialog.MinutesBox.Value);

            return null;
        }

        private static void PlayNotification(int remaining = -1)
        {
            using SoundPlayer audio = new();
            audio.Stream = remaining switch
            {
                30 => Properties.Resources._30_minutes,
                10 => Properties.Resources._10_minutes,
                0 => Properties.Resources.time_reached,
                _ => Properties.Resources.generic_notification
            };
            
            audio.Play();
        }

        private static string Format(TimeSpan time)
        {
            int hours = (int)time.TotalHours;
            int minutes = time.Minutes;
            return (hours == 0 ? "" : hours + "h")
                   + (hours != 0 && minutes != 0 ? " " : "")
                   + (minutes == 0 && hours != 0 ? "" : minutes + "min");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                if (!PcLocker.Locked)
                    Opacity = 0;
            }
            else
            {

                PassedTimeWatcher.SaveToConfig();
                Logger.Log($"Closing program. Reason: {e.CloseReason}");
            }

            HomeAssistantMqtt.Instance.Close();
            base.OnFormClosing(e);
        }



        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.MqttEnabled)
            { //HomeAssistantMqtt.Instance.Register();
            }

            ConfigureIdleProgressBar();

        }

        private void AppButt_Click(object sender, EventArgs e)
        {
#if !DEBUG
                if (!RequestPassword())
                    return;
#endif
            Process.Start(Program.RootDirectory);
        }

        private void LogButt_Click(object sender, EventArgs e)
        {
            if (File.Exists(Logger.LogPath))
                Process.Start(Logger.LogPath);
        }

        private void RestartButt_Click(object sender, EventArgs e)
        {
            Logger.Log("Restart button clicked - restarting.");
            Utils.StartWithParameters(
                Assembly.GetEntryAssembly()!.Location,
                $"{Program.ConsoleActions[Program.ConsoleAction.Open]}");

            Application.Exit();
            /*if (await Updater.IsUpdateAvailable())
                Updater.DownloadLatestUpdateAsync(UpdateHandler);
            else
                MessageBox.Show("Používáte nejnovější verzi!", "Žádné aktualizace");*/
        }



        private void ChangePasswordButt_Click(object sender, EventArgs e)
        {


        }

        private void ToggleButt_Click(object sender, EventArgs e)
        {

        }

        private void ChangeIdleTimeButt_Click(object sender, EventArgs e)
        {

        }

        private void ConfigureIdleProgressBar()
        {
            pbIdle.Maximum = (int)PassedTimeWatcher.IdleThreshold.TotalSeconds;
            pbIdle.Minimum = 0;
            pbIdle.Value = 0;
        }
        private void ChangeResetHourButt_Click(object sender, EventArgs e)
        {

        }

        private void ChangeMaxButt_Click(object sender, EventArgs e)
        {


        }

        private void ChangePassedButt_Click(object sender, EventArgs e)
        {
            TimeSpan? time = ObtainTimeOrNull(TimeSpan.FromMilliseconds(PassedTimeWatcher.PassedMillis));
            if (!time.HasValue || !RequestPassword())
                return;

            PassedTimeWatcher.PassedMillis = (int)time.Value.TotalMilliseconds;
            if (PassedTimeWatcher.PassedMillis < PassedTimeWatcher.MaxTime.TotalMilliseconds)
                UnlockIfLocked();
        }

        private void DumpButt_Click(object sender, EventArgs e)
        {
            Logger.Log($"DUMP:\n" +
           $"  Idle time during sleep: {Utils.FormatTime(PassedTimeWatcher.IdleMillisDuringSleep)}\n" +
           $"  Last idle time: {Utils.FormatTime(PassedTimeWatcher.LastIdleTimeMillis)}\n", true);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.TimeLbl = new System.Windows.Forms.Label();
            this.StatusLbl = new System.Windows.Forms.Label();
            this.LogButt = new System.Windows.Forms.Button();
            this.AppButt = new System.Windows.Forms.Button();
            this.DumpButt = new System.Windows.Forms.Button();
            this.IdleLbl = new System.Windows.Forms.Label();
            this.btnMqtt = new System.Windows.Forms.Button();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripProgressBar1 = new System.Windows.Forms.ToolStripProgressBar();
            this.versionLbl = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripSplitButton1 = new System.Windows.Forms.ToolStripSplitButton();
            this.lblSlot = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.lblCurrentSlot = new System.Windows.Forms.Label();
            this.pbIdle = new System.Windows.Forms.ProgressBar();
            this.label2 = new System.Windows.Forms.Label();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.restartToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.settingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.changeElapsedTimeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.changeTimeOfDayToResetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.changeMaxAllowedTimeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.changePasswordToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.changeIdleTimeNeededToStopToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pauseStartToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // TimeLbl
            // 
            this.TimeLbl.Font = new System.Drawing.Font("Microsoft YaHei UI", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.TimeLbl.Location = new System.Drawing.Point(10, 1);
            this.TimeLbl.Name = "TimeLbl";
            this.TimeLbl.Size = new System.Drawing.Size(415, 81);
            this.TimeLbl.TabIndex = 0;
            this.TimeLbl.Text = "Time:";
            this.TimeLbl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // StatusLbl
            // 
            this.StatusLbl.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.StatusLbl.Location = new System.Drawing.Point(85, 82);
            this.StatusLbl.Name = "StatusLbl";
            this.StatusLbl.Size = new System.Drawing.Size(144, 23);
            this.StatusLbl.TabIndex = 0;
            this.StatusLbl.Text = "Status";
            this.StatusLbl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.StatusLbl.Click += new System.EventHandler(this.StatusLbl_Click);
            // 
            // LogButt
            // 
            this.LogButt.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.LogButt.Location = new System.Drawing.Point(330, 483);
            this.LogButt.Name = "LogButt";
            this.LogButt.Size = new System.Drawing.Size(51, 23);
            this.LogButt.TabIndex = 9;
            this.LogButt.Text = "Log";
            this.LogButt.UseVisualStyleBackColor = true;
            this.LogButt.Click += new System.EventHandler(this.LogButt_Click);
            // 
            // AppButt
            // 
            this.AppButt.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.AppButt.Location = new System.Drawing.Point(273, 483);
            this.AppButt.Name = "AppButt";
            this.AppButt.Size = new System.Drawing.Size(51, 23);
            this.AppButt.TabIndex = 10;
            this.AppButt.Text = "App";
            this.AppButt.UseVisualStyleBackColor = true;
            this.AppButt.Visible = false;
            this.AppButt.Click += new System.EventHandler(this.AppButt_Click);
            // 
            // DumpButt
            // 
            this.DumpButt.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.DumpButt.Location = new System.Drawing.Point(3, 523);
            this.DumpButt.Name = "DumpButt";
            this.DumpButt.Size = new System.Drawing.Size(10, 12);
            this.DumpButt.TabIndex = 11;
            this.DumpButt.Text = "Dump";
            this.DumpButt.UseVisualStyleBackColor = true;
            this.DumpButt.Click += new System.EventHandler(this.DumpButt_Click);
            // 
            // IdleLbl
            // 
            this.IdleLbl.Location = new System.Drawing.Point(106, 480);
            this.IdleLbl.Name = "IdleLbl";
            this.IdleLbl.Size = new System.Drawing.Size(161, 23);
            this.IdleLbl.TabIndex = 14;
            this.IdleLbl.Text = "time away from the computer";
            this.IdleLbl.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.IdleLbl.Click += new System.EventHandler(this.IdleLbl_Click);
            // 
            // btnMqtt
            // 
            this.btnMqtt.Location = new System.Drawing.Point(34, 480);
            this.btnMqtt.Name = "btnMqtt";
            this.btnMqtt.Size = new System.Drawing.Size(74, 19);
            this.btnMqtt.TabIndex = 15;
            this.btnMqtt.Text = "MQTT";
            this.btnMqtt.UseVisualStyleBackColor = true;
            this.btnMqtt.Click += new System.EventHandler(this.btnMqtt_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1,
            this.toolStripProgressBar1,
            this.versionLbl,
            this.toolStripSplitButton1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 515);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(439, 22);
            this.statusStrip1.TabIndex = 16;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(118, 17);
            this.toolStripStatusLabel1.Text = "toolStripStatusLabel1";
            // 
            // toolStripProgressBar1
            // 
            this.toolStripProgressBar1.Name = "toolStripProgressBar1";
            this.toolStripProgressBar1.Size = new System.Drawing.Size(100, 16);
            // 
            // versionLbl
            // 
            this.versionLbl.AutoToolTip = true;
            this.versionLbl.Name = "versionLbl";
            this.versionLbl.Size = new System.Drawing.Size(31, 17);
            this.versionLbl.Text = "x.x.x";
            this.versionLbl.ToolTipText = "The version of the app";
            // 
            // toolStripSplitButton1
            // 
            this.toolStripSplitButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripSplitButton1.Image = ((System.Drawing.Image)(resources.GetObject("toolStripSplitButton1.Image")));
            this.toolStripSplitButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripSplitButton1.Name = "toolStripSplitButton1";
            this.toolStripSplitButton1.Size = new System.Drawing.Size(32, 20);
            this.toolStripSplitButton1.Text = "toolStripSplitButton1";
            // 
            // lblSlot
            // 
            this.lblSlot.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.lblSlot.Location = new System.Drawing.Point(12, 121);
            this.lblSlot.Name = "lblSlot";
            this.lblSlot.Size = new System.Drawing.Size(176, 23);
            this.lblSlot.TabIndex = 17;
            this.lblSlot.Text = "LastSlot";
            this.lblSlot.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.label1.Location = new System.Drawing.Point(13, 82);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(60, 20);
            this.label1.TabIndex = 18;
            this.label1.Text = "Status:";
            // 
            // lblCurrentSlot
            // 
            this.lblCurrentSlot.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.lblCurrentSlot.Location = new System.Drawing.Point(228, 121);
            this.lblCurrentSlot.Name = "lblCurrentSlot";
            this.lblCurrentSlot.Size = new System.Drawing.Size(176, 23);
            this.lblCurrentSlot.TabIndex = 19;
            this.lblCurrentSlot.Text = "CurrentSlot";
            this.lblCurrentSlot.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pbIdle
            // 
            this.pbIdle.Location = new System.Drawing.Point(35, 454);
            this.pbIdle.Name = "pbIdle";
            this.pbIdle.Size = new System.Drawing.Size(368, 23);
            this.pbIdle.TabIndex = 20;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(35, 435);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(62, 13);
            this.label2.TabIndex = 21;
            this.label2.Text = "Time to Idle";
            this.label2.Click += new System.EventHandler(this.label2_Click);
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.settingsToolStripMenuItem,
            this.aboutToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(439, 24);
            this.menuStrip1.TabIndex = 22;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.restartToolStripMenuItem,
            this.pauseStartToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(110, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // restartToolStripMenuItem
            // 
            this.restartToolStripMenuItem.Name = "restartToolStripMenuItem";
            this.restartToolStripMenuItem.Size = new System.Drawing.Size(110, 22);
            this.restartToolStripMenuItem.Text = "Restart";
            this.restartToolStripMenuItem.Click += new System.EventHandler(this.restartToolStripMenuItem_Click);
            // 
            // settingsToolStripMenuItem
            // 
            this.settingsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.changeElapsedTimeToolStripMenuItem,
            this.changeTimeOfDayToResetToolStripMenuItem,
            this.changeMaxAllowedTimeToolStripMenuItem,
            this.changePasswordToolStripMenuItem,
            this.changeIdleTimeNeededToStopToolStripMenuItem});
            this.settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            this.settingsToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            this.settingsToolStripMenuItem.Text = "Settings";
            // 
            // changeElapsedTimeToolStripMenuItem
            // 
            this.changeElapsedTimeToolStripMenuItem.Name = "changeElapsedTimeToolStripMenuItem";
            this.changeElapsedTimeToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
            this.changeElapsedTimeToolStripMenuItem.Text = "Change elapsed time";
            this.changeElapsedTimeToolStripMenuItem.Click += new System.EventHandler(this.changeElapsedTimeToolStripMenuItem_Click);
            // 
            // changeTimeOfDayToResetToolStripMenuItem
            // 
            this.changeTimeOfDayToResetToolStripMenuItem.Name = "changeTimeOfDayToResetToolStripMenuItem";
            this.changeTimeOfDayToResetToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
            this.changeTimeOfDayToResetToolStripMenuItem.Text = "Change time of day to reset";
            this.changeTimeOfDayToResetToolStripMenuItem.Click += new System.EventHandler(this.changeTimeOfDayToResetToolStripMenuItem_Click);
            // 
            // changeMaxAllowedTimeToolStripMenuItem
            // 
            this.changeMaxAllowedTimeToolStripMenuItem.Name = "changeMaxAllowedTimeToolStripMenuItem";
            this.changeMaxAllowedTimeToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
            this.changeMaxAllowedTimeToolStripMenuItem.Text = "Change max allowed time";
            this.changeMaxAllowedTimeToolStripMenuItem.Click += new System.EventHandler(this.changeMaxAllowedTimeToolStripMenuItem_Click);
            // 
            // changePasswordToolStripMenuItem
            // 
            this.changePasswordToolStripMenuItem.Name = "changePasswordToolStripMenuItem";
            this.changePasswordToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.changePasswordToolStripMenuItem.Text = "Change Password";
            this.changePasswordToolStripMenuItem.Click += new System.EventHandler(this.changePasswordToolStripMenuItem_Click);
            // 
            // changeIdleTimeNeededToStopToolStripMenuItem
            // 
            this.changeIdleTimeNeededToStopToolStripMenuItem.Name = "changeIdleTimeNeededToStopToolStripMenuItem";
            this.changeIdleTimeNeededToStopToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.changeIdleTimeNeededToStopToolStripMenuItem.Text = "Change idle time needed to stop";
            this.changeIdleTimeNeededToStopToolStripMenuItem.Click += new System.EventHandler(this.changeIdleTimeNeededToStopToolStripMenuItem_Click);
            // 
            // pauseStartToolStripMenuItem
            // 
            this.pauseStartToolStripMenuItem.Name = "pauseStartToolStripMenuItem";
            this.pauseStartToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.pauseStartToolStripMenuItem.Text = "Pause/Start";
            this.pauseStartToolStripMenuItem.Click += new System.EventHandler(this.pauseStartToolStripMenuItem_Click);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(52, 20);
            this.aboutToolStripMenuItem.Text = "About";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(439, 537);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.pbIdle);
            this.Controls.Add(this.lblCurrentSlot);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lblSlot);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Controls.Add(this.btnMqtt);
            this.Controls.Add(this.IdleLbl);
            this.Controls.Add(this.DumpButt);
            this.Controls.Add(this.AppButt);
            this.Controls.Add(this.LogButt);
            this.Controls.Add(this.StatusLbl);
            this.Controls.Add(this.TimeLbl);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MainMenuStrip = this.menuStrip1;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MainForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Digital well-being";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label IdleLbl;

        private System.Windows.Forms.Button DumpButt;

        private System.Windows.Forms.Button AppButt;

        private System.Windows.Forms.Button LogButt;

        private System.Windows.Forms.Label StatusLbl;

        private System.Windows.Forms.Label TimeLbl;

        #endregion

        private void IdleLbl_Click(object sender, EventArgs e)
        {

        }

        private void btnMqtt_Click(object sender, EventArgs e)
        {
            var mqttForm = new MqttForm();
            mqttForm.Show();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void StatusLbl_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (RequestPassword())
                Application.Exit();
        }

        private void restartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Logger.Log("Restart button clicked - restarting.");
            Utils.StartWithParameters(
                Assembly.GetEntryAssembly()!.Location,
                $"{Program.ConsoleActions[Program.ConsoleAction.Open]}");

            Application.Exit();
            /*if (await Updater.IsUpdateAvailable())
                Updater.DownloadLatestUpdateAsync(UpdateHandler);
            else
                MessageBox.Show("Používáte nejnovější verzi!", "Žádné aktualizace");*/
        }

        private void changeElapsedTimeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TimeSpan? time = ObtainTimeOrNull(TimeSpan.FromMilliseconds(PassedTimeWatcher.PassedMillis));
            if (!time.HasValue || !RequestPassword())
                return;

            PassedTimeWatcher.PassedMillis = (int)time.Value.TotalMilliseconds;
            if (PassedTimeWatcher.PassedMillis < PassedTimeWatcher.MaxTime.TotalMilliseconds)
                UnlockIfLocked();
        }

        private void changeTimeOfDayToResetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TimeSpan currHour = TimeSpan.FromHours(ResetChecker.ResetHour);
            TimeSpan? hour = ObtainTimeOrNull(currHour);
            if (!hour.HasValue || hour == currHour || !RequestPassword())
                return;

            ResetChecker.ResetHour = (byte)hour.Value.Hours;
            Config.SetValue(Config.Property.ResetHour, hour.Value.Hours);
        }

        private void changeMaxAllowedTimeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TimeSpan? time = ObtainTimeOrNull(PassedTimeWatcher.MaxTime);

            if (!time.HasValue || !RequestPassword())
                return;

            Config.SetValue(Config.Property.MaxTimeMins, (int)time.Value.TotalMinutes);
            PassedTimeWatcher.MaxTime = time.Value;

            if (PassedTimeWatcher.PassedMillis < PassedTimeWatcher.MaxTime.TotalMilliseconds)
                UnlockIfLocked();
        }

        private void changeIdleTimeNeededToStopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TimeSpan currTime = PassedTimeWatcher.IdleThreshold;
            TimeSpan? time = ObtainTimeOrNull(currTime);
            if (time is not null && RequestPassword())
            {
                PassedTimeWatcher.IdleThreshold = time.Value;
                Config.SetValue(Config.Property.IdleThresholdMins, (int)time.Value.TotalMinutes);
            }
            ConfigureIdleProgressBar();
        }

        private void changePasswordToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string? newPassword = ObtainTextOrNull(Properties.Resources.NewPassword, true);

            if (newPassword is null || !RequestPassword())
                return;

            Config.SetValue(Config.Property.Password, newPassword);
            Password = newPassword;
        }

        private void pauseStartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!RequestPassword())
                return;

            PassedTimeWatcher.Running = !PassedTimeWatcher.Running;
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {

        }
    }
}