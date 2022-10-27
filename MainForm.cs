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

namespace Wellbeing
{
    public partial class MainForm : Form
    {
        private const byte DefaultResetHour = 3;
        private const byte DefaultMaxTimeMins = 240;
        private const byte DefaultIdleThresholdMins = 6;
        private const string DateTimeFormatter = "G";
        private const string DefaultPassword = "17861177";
        
        private readonly ResetChecker ResetChecker;
        private readonly UpdateChecker UpdateChecker;
        private readonly PcLocker PcLocker;
        private int LastShownMins = int.MaxValue;
        private string? Password;
        
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
            string userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            var pc= System.Net.Dns.GetHostName();
            //MqttClient.Instance.Send("test");

            MqttClient.Instance.Send("wellbeing/v-tiagoand-lp3/tiagoand", "{date:"+DateTime.Now+", status:on, time:40}");
            MqttClient.Instance.Send("wellbeing/v-tiagoand-lp3/tiagoand", "{date:" + DateTime.Now + ", status:off, time:40}");
            MqttClient.Instance.Send("wellbeing/slapc/kikoberlenga", "{date:" + DateTime.Now + ", status:on, time:41}");


            Thread.CurrentThread.CurrentUICulture = CultureInfo.InstalledUICulture;
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("pt-PT");
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            resources.ApplyResources(this, "$this");

            InitializeComponent();
            SetButtonListeners();
            versionLbl.Text = Program.Version;

            DateTime lastOpen = Config.GetDateTime(Config.Property.LastOpenOrResetDateTime, DateTimeFormatter) ?? DateTime.MinValue;
            int resetHour = Config.GetIntOrNull(Config.Property.ResetHour) ?? DefaultResetHour;
            ResetChecker = new(resetHour, lastOpen);
            
            Password = Config.GetValueOrNull(Config.Property.Password) ?? DefaultPassword;
            UpdateChecker = new();
            PcLocker = new(this);

            ResetChecker.ShouldResetHandler += (_, _) => Reset();
            UpdateChecker.OnUpdateAvailable += (_, _) =>
            {
                Updater.DownloadLatestUpdateAsync(UpdateHandler);
            };
            
            PassedTimeWatcher.OnRunningChanged += (_, running) =>
                Invoke(new EventHandler((_, _) => StatusLbl.Text = running ? Properties.Resources.On : Properties.Resources.Suspended));
            PassedTimeWatcher.OnUpdate += (_, time) =>
                Invoke(new EventHandler((_, _) => HandleTick(time.passedMillis, time.remainingMillis)));
            PassedTimeWatcher.OnMaxTimeReached += (_, _) =>
                Invoke(new EventHandler((_, _) => HandleMaxTimeReached()));

            int passedSecsToday = Config.GetIntOrNull(Config.Property.PassedTodaySecs) ?? 0;
            PassedTimeWatcher.PassedMillis = (int)TimeSpan.FromSeconds(passedSecsToday).TotalMilliseconds;
            PassedTimeWatcher.MaxTime = TimeSpan.FromMinutes(Config.GetIntOrNull(Config.Property.MaxTimeMins) ?? DefaultMaxTimeMins);
            PassedTimeWatcher.IdleThreshold = TimeSpan.FromMinutes(Config.GetIntOrNull(Config.Property.IdleThresholdMins) ?? DefaultIdleThresholdMins);
            
            Config.SetValue(Config.Property.LastOpenOrResetDateTime, DateTime.Now.ToString(DateTimeFormatter));
        }

        private static void UpdateHandler(Action update)
        {
            PassedTimeWatcher.SaveToConfig();
            update();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            if (ResetChecker.ShouldResetPassedTime())
                Reset();

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
            Logger.Log("Resetting passed time.");
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

        private void HandleTick(int passedMillis, int remainingMillis)
        {
            if (Opacity != 0)
                IdleLbl.Text = Utils.FormatTime(IdleTimeWatcher.IdleTimeMillis);
            TimeSpan passedTime = TimeSpan.FromMilliseconds(passedMillis);
            TimeSpan remainingTime = TimeSpan.FromMilliseconds(remainingMillis);
            string formatted = Properties.Resources.Time+": " + Format(passedTime) + " / " + Format(PassedTimeWatcher.MaxTime);
            TimeLbl.Text = formatted;

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
            int rightSideXPos = Location.X + Width;
            int bottomSideYPos = Location.Y + Height;
            
            int screenMaxY = Screen.PrimaryScreen.Bounds.Height;
            int screenMaxX = Screen.PrimaryScreen.Bounds.Width;

            if (rightSideXPos > screenMaxX)
                Location = Location with { X = screenMaxX - Width };
            if (bottomSideYPos > screenMaxY)
                Location = Location with { Y = screenMaxY - Height };
            if (Location.X < 0)
                Location = Location with { X = 0 };
            if (Location.Y < 0)
                Location = Location with { Y = 0 };
        }

        private void SetButtonListeners()
        {
            CloseButt.Click += (_, _) =>
            {
                if (RequestPassword())
                    Application.Exit();
            };

            ChangeIdleTimeButt.Click += (_, _) =>
            {
                TimeSpan currTime = PassedTimeWatcher.IdleThreshold;
                TimeSpan? time = ObtainTimeOrNull(currTime);
                if (time is not null && RequestPassword())
                {
                    PassedTimeWatcher.IdleThreshold = time.Value;
                    Config.SetValue(Config.Property.IdleThresholdMins, (int)time.Value.TotalMinutes);
                }
            };

            ChangeResetHourButt.Click += (_, _) =>
            {
                TimeSpan currHour = TimeSpan.FromHours(ResetChecker.ResetHour);
                TimeSpan? hour = ObtainTimeOrNull(currHour);
                if (!hour.HasValue || hour == currHour || !RequestPassword())
                    return;
                
                ResetChecker.ResetHour = (byte)hour.Value.Hours;
                Config.SetValue(Config.Property.ResetHour, hour.Value.Hours);
            };

            ToggleButt.Click += (_, _) =>
            {
                if (!RequestPassword())
                    return;

                PassedTimeWatcher.Running = !PassedTimeWatcher.Running;
            };

            ChangePassedButt.Click += (_, _) =>
            {
                TimeSpan? time = ObtainTimeOrNull(TimeSpan.FromMilliseconds(PassedTimeWatcher.PassedMillis));
                if (!time.HasValue || !RequestPassword())
                    return;
                
                PassedTimeWatcher.PassedMillis = (int)time.Value.TotalMilliseconds;
                if (PassedTimeWatcher.PassedMillis < PassedTimeWatcher.MaxTime.TotalMilliseconds)
                    UnlockIfLocked();
            };

            ChangeMaxButt.Click += (_, _) =>
            {
                TimeSpan? time = ObtainTimeOrNull(PassedTimeWatcher.MaxTime);
                
                if (!time.HasValue || !RequestPassword())
                    return;
                
                Config.SetValue(Config.Property.MaxTimeMins, (int)time.Value.TotalMinutes);
                PassedTimeWatcher.MaxTime = time.Value;
                
                if (PassedTimeWatcher.PassedMillis < PassedTimeWatcher.MaxTime.TotalMilliseconds)
                    UnlockIfLocked();
            };

            ChangePasswordButt.Click += (_, _) =>
            {
                string? newPassword = ObtainTextOrNull(Properties.Resources.NewPassword, true);
                
                if (newPassword is null || !RequestPassword())
                    return;
                
                Config.SetValue(Config.Property.Password, newPassword);
                Password = newPassword;
            };



            DumpButt.Click += (_, _) =>
            {
                Logger.Log($"DUMP:\n" +
                           $"  Idle time during sleep: {Utils.FormatTime(PassedTimeWatcher.IdleMillisDuringSleep)}\n" +
                           $"  Last idle time: {Utils.FormatTime(PassedTimeWatcher.LastIdleTimeMillis)}\n");
            };

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

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.TimeLbl = new System.Windows.Forms.Label();
            this.CloseButt = new System.Windows.Forms.Button();
            this.ChangeMaxButt = new System.Windows.Forms.Button();
            this.ChangePassedButt = new System.Windows.Forms.Button();
            this.ToggleButt = new System.Windows.Forms.Button();
            this.StatusLbl = new System.Windows.Forms.Label();
            this.ChangePasswordButt = new System.Windows.Forms.Button();
            this.ChangeResetHourButt = new System.Windows.Forms.Button();
            this.ChangeIdleTimeButt = new System.Windows.Forms.Button();
            this.versionLbl = new System.Windows.Forms.Label();
            this.LogButt = new System.Windows.Forms.Button();
            this.AppButt = new System.Windows.Forms.Button();
            this.DumpButt = new System.Windows.Forms.Button();
            this.RestartButt = new System.Windows.Forms.Button();
            this.IdleLbl = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // TimeLbl
            // 
            this.TimeLbl.Font = new System.Drawing.Font("Microsoft YaHei UI", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.TimeLbl.Location = new System.Drawing.Point(12, 9);
            this.TimeLbl.Name = "TimeLbl";
            this.TimeLbl.Size = new System.Drawing.Size(415, 81);
            this.TimeLbl.TabIndex = 0;
            this.TimeLbl.Text = "Time:";
            this.TimeLbl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // CloseButt
            // 
            this.CloseButt.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.CloseButt.Location = new System.Drawing.Point(34, 421);
            this.CloseButt.Name = "CloseButt";
            this.CloseButt.Size = new System.Drawing.Size(171, 58);
            this.CloseButt.TabIndex = 6;
            this.CloseButt.Text = global::Wellbeing.Properties.Resources.Terminate;
            this.CloseButt.UseVisualStyleBackColor = true;
            this.CloseButt.Click += new System.EventHandler(this.CloseButt_Click);
            // 
            // ChangeMaxButt
            // 
            this.ChangeMaxButt.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.ChangeMaxButt.Location = new System.Drawing.Point(232, 165);
            this.ChangeMaxButt.Name = "ChangeMaxButt";
            this.ChangeMaxButt.Size = new System.Drawing.Size(171, 58);
            this.ChangeMaxButt.TabIndex = 2;
            this.ChangeMaxButt.Text = global::Wellbeing.Properties.Resources.ChangeMaxButt;
            this.ChangeMaxButt.UseVisualStyleBackColor = true;
            this.ChangeMaxButt.Click += new System.EventHandler(this.ChangeMaxButt_Click);
            // 
            // ChangePassedButt
            // 
            this.ChangePassedButt.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.ChangePassedButt.Location = new System.Drawing.Point(34, 165);
            this.ChangePassedButt.Name = "ChangePassedButt";
            this.ChangePassedButt.Size = new System.Drawing.Size(171, 58);
            this.ChangePassedButt.TabIndex = 1;
            this.ChangePassedButt.Text = global::Wellbeing.Properties.Resources.ChangePassedButt;
            this.ChangePassedButt.UseVisualStyleBackColor = true;
            this.ChangePassedButt.Click += new System.EventHandler(this.ChangePassedButt_Click);
            // 
            // ToggleButt
            // 
            this.ToggleButt.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.ToggleButt.Location = new System.Drawing.Point(34, 333);
            this.ToggleButt.Name = "ToggleButt";
            this.ToggleButt.Size = new System.Drawing.Size(171, 58);
            this.ToggleButt.TabIndex = 5;
            this.ToggleButt.Text = "Pause/Start";
            this.ToggleButt.UseVisualStyleBackColor = true;
            this.ToggleButt.Click += new System.EventHandler(this.ToggleButt_Click);
            // 
            // StatusLbl
            // 
            this.StatusLbl.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.StatusLbl.Location = new System.Drawing.Point(16, 73);
            this.StatusLbl.Name = "StatusLbl";
            this.StatusLbl.Size = new System.Drawing.Size(144, 23);
            this.StatusLbl.TabIndex = 0;
            this.StatusLbl.Text = "Status";
            this.StatusLbl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // ChangePasswordButt
            // 
            this.ChangePasswordButt.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.ChangePasswordButt.Location = new System.Drawing.Point(232, 333);
            this.ChangePasswordButt.Name = "ChangePasswordButt";
            this.ChangePasswordButt.Size = new System.Drawing.Size(171, 58);
            this.ChangePasswordButt.TabIndex = 4;
            this.ChangePasswordButt.Text = global::Wellbeing.Properties.Resources.ChangePass;
            this.ChangePasswordButt.UseVisualStyleBackColor = true;
            this.ChangePasswordButt.Click += new System.EventHandler(this.ChangePasswordButt_Click);
            // 
            // ChangeResetHourButt
            // 
            this.ChangeResetHourButt.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.ChangeResetHourButt.Location = new System.Drawing.Point(34, 248);
            this.ChangeResetHourButt.Name = "ChangeResetHourButt";
            this.ChangeResetHourButt.Size = new System.Drawing.Size(171, 58);
            this.ChangeResetHourButt.TabIndex = 3;
            this.ChangeResetHourButt.Text = global::Wellbeing.Properties.Resources.ChangeResetHour;
            this.ChangeResetHourButt.UseVisualStyleBackColor = true;
            this.ChangeResetHourButt.Click += new System.EventHandler(this.ChangeResetHourButt_Click);
            // 
            // ChangeIdleTimeButt
            // 
            this.ChangeIdleTimeButt.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.ChangeIdleTimeButt.Location = new System.Drawing.Point(232, 248);
            this.ChangeIdleTimeButt.Name = "ChangeIdleTimeButt";
            this.ChangeIdleTimeButt.Size = new System.Drawing.Size(171, 58);
            this.ChangeIdleTimeButt.TabIndex = 7;
            this.ChangeIdleTimeButt.Text = global::Wellbeing.Properties.Resources.ChangeIdleTime;
            this.ChangeIdleTimeButt.UseVisualStyleBackColor = true;
            this.ChangeIdleTimeButt.Click += new System.EventHandler(this.ChangeIdleTimeButt_Click);
            // 
            // versionLbl
            // 
            this.versionLbl.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.versionLbl.Location = new System.Drawing.Point(386, 502);
            this.versionLbl.Name = "versionLbl";
            this.versionLbl.Size = new System.Drawing.Size(38, 23);
            this.versionLbl.TabIndex = 8;
            this.versionLbl.Text = "x.x.x";
            this.versionLbl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // LogButt
            // 
            this.LogButt.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.LogButt.Location = new System.Drawing.Point(329, 502);
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
            this.AppButt.Location = new System.Drawing.Point(272, 502);
            this.AppButt.Name = "AppButt";
            this.AppButt.Size = new System.Drawing.Size(51, 23);
            this.AppButt.TabIndex = 10;
            this.AppButt.Text = "App";
            this.AppButt.UseVisualStyleBackColor = true;
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
            // 
            // RestartButt
            // 
            this.RestartButt.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.RestartButt.Location = new System.Drawing.Point(232, 421);
            this.RestartButt.Name = "RestartButt";
            this.RestartButt.Size = new System.Drawing.Size(171, 58);
            this.RestartButt.TabIndex = 12;
            this.RestartButt.Text = global::Wellbeing.Properties.Resources.Restart;
            this.RestartButt.UseVisualStyleBackColor = true;
            this.RestartButt.Click += new System.EventHandler(this.RestartButt_Click);
            // 
            // IdleLbl
            // 
            this.IdleLbl.Location = new System.Drawing.Point(105, 502);
            this.IdleLbl.Name = "IdleLbl";
            this.IdleLbl.Size = new System.Drawing.Size(161, 23);
            this.IdleLbl.TabIndex = 14;
            this.IdleLbl.Text = "-- čas mimo počítač";
            this.IdleLbl.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(439, 537);
            this.Controls.Add(this.IdleLbl);
            this.Controls.Add(this.RestartButt);
            this.Controls.Add(this.DumpButt);
            this.Controls.Add(this.AppButt);
            this.Controls.Add(this.LogButt);
            this.Controls.Add(this.versionLbl);
            this.Controls.Add(this.ChangeIdleTimeButt);
            this.Controls.Add(this.ChangeResetHourButt);
            this.Controls.Add(this.ChangePasswordButt);
            this.Controls.Add(this.StatusLbl);
            this.Controls.Add(this.ToggleButt);
            this.Controls.Add(this.ChangePassedButt);
            this.Controls.Add(this.ChangeMaxButt);
            this.Controls.Add(this.CloseButt);
            this.Controls.Add(this.TimeLbl);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MainForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Digital well-being";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.Label IdleLbl;

        private System.Windows.Forms.Button RestartButt;

        private System.Windows.Forms.Button DumpButt;

        private System.Windows.Forms.Button AppButt;

        private System.Windows.Forms.Button LogButt;

        private System.Windows.Forms.Label versionLbl;

        private System.Windows.Forms.Button ChangeIdleTimeButt;

        private System.Windows.Forms.Button ChangeResetHourButt;

        private System.Windows.Forms.Button ChangePasswordButt;

        private System.Windows.Forms.Label StatusLbl;

        private System.Windows.Forms.Button ToggleButt;

        private System.Windows.Forms.Button CloseButt;
        private System.Windows.Forms.Button ChangeMaxButt;
        private System.Windows.Forms.Button ChangePassedButt;

        private System.Windows.Forms.Label TimeLbl;

        #endregion

        private void MainForm_Load(object sender, EventArgs e)
        {

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

        private void CloseButt_Click(object sender, EventArgs e)
        {

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

        private void ChangeResetHourButt_Click(object sender, EventArgs e)
        {

        }

        private void ChangeMaxButt_Click(object sender, EventArgs e)
        {

        }

        private void ChangePassedButt_Click(object sender, EventArgs e)
        {

        }
    }
}