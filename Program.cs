﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Wellbeing.Properties;

namespace Wellbeing
{
    internal static class Program
    {
        public enum ConsoleAction { Delete, Open };

        public const string Version = "2.0.8";
        public static readonly Dictionary<ConsoleAction, string> ConsoleActions = new()
        {
            { ConsoleAction.Delete , "--delete" },
            { ConsoleAction.Open , "--open" }
        };
        private static readonly NotifyIcon PinNotifyIcon = new()
        {
            Text = "Click to open/shut",
            Icon = Resources.program_icon,
            Visible = true
        };

        public static readonly string RootDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wellbeing");
        public const string ExeName = "Wellbeing.exe";

        /// <summary>The main entry point for the application.</summary>
        [STAThread]
        private static void Main(string[] args)
        {

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(MyHandler);

            //Check if single instance per user of the App http://getgoingit.blogspot.com/2016/05/single-instance-per-user-of-desktop.html
            String mutexName = "Wellbeing" +
            System.Security.Principal.WindowsIdentity.GetCurrent().User.AccountDomainSid;

            Boolean createdNew;

            Mutex mutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew) {
                Logger.Log("Mutex Instance is not single, closing.");
                return;
            }

            if (!Directory.Exists(RootDirectory))
            {
                Logger.Log("Program directory did not exist, creating.");
                Directory.CreateDirectory(RootDirectory);
            }

            bool open = false;
            for (int i = 0; i < args.Length; ++i)
            {
                if (args[i] == ConsoleActions[ConsoleAction.Delete])
                {
                    Logger.Log("deleting (based on console argument): " + (args.Length > i+1 ? args[i + 1] : "no path specified"));
                    HandleDeleteArgument(args, i);
                    i += 1;
                    continue;
                }

                if (args[i] == ConsoleActions[ConsoleAction.Open])
                {
                    open = true;
                    continue;
                }
                
                Logger.Log("Unrecognized console argument.");
            }
            
            Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);

#if !DEBUG
            bool replaced = false;
            if (ShouldReplaceCurrent())
            {
                Logger.Log("Replacing current.");
                ReplaceCurrent();
                replaced = true;
            }

            if (replaced || ShouldOpenMainExe())
            {
                Logger.Log("Starting new instance and closing this one.");
                Utils.StartWithParameters(
                    StartupLauncher.ExecutablePath,
                    $"{ConsoleActions[ConsoleAction.Delete]} \"{Application.ExecutablePath}\"");
                return;
            }
#endif
            //if (!CheckIsSingleInstance())
            //{
            //    Logger.Log("Instance is not single, closing.");
            //    return;
            //}
                
            //StartupLauncher.ExcludeFromDefender();
            Logger.Log("Set launch on startup registry value.");
            StartupLauncher.SetLaunchOnStartup();
            
            var mainForm = InitMainForm();
            if (open)
                mainForm.Opacity = 1;
            Application.Run(mainForm);
        }

        private static void HandleDeleteArgument(IReadOnlyList<string> args, int index)
        {
            if (index+1 == args.Count || args[index+1].StartsWith("-"))
                Logger.Log("Invalid console arguments.");

            string filename = args[index+1];
            if (File.Exists(filename))
                TryDeleteRecursively(filename);
            else
                Logger.Log("File path doesn't exist");
        }

        /// <summary>
        /// Keeps trying to delete the file for a few seconds. Doesn't throw an exception.
        /// </summary>
        /// <param name="filename">The path of the file to delete.</param>
        /// <returns></returns>
        private static bool TryDeleteRecursively(string filename)
        {
            for (int i = 0; i < 15; ++i)
            {
                try
                {
                    File.Delete(filename);
                    return true;
                }
                catch (Exception e)
                {
                    Logger.Log("Ignoring exception while recursicely deleting (expected, process migt not be closed yet).");
                    Thread.Sleep(200);
                }   
            }

            return false;
        }

        private static MainForm InitMainForm()
        {
            var mainForm = new MainForm();
            mainForm.Opacity = 0;
            PinNotifyIcon.Click += (_, _) =>
            {
                mainForm.Opacity = mainForm.Opacity == 0 ? 1 : 0;
                if ((int)mainForm.Opacity != 1)
                    return;
                
                mainForm.WindowState = FormWindowState.Minimized;
                mainForm.WindowState = FormWindowState.Normal;
            };
            return mainForm;
        }

        /// <summary>
        /// Checks if this process is the only instance of this program with a few secs of polling.
        /// </summary>
        /// <returns>True if this exe is the only instance of this program. False otherwise.</returns>
        private static bool CheckIsSingleInstance()
        {
            for (int i = 0; i < 3; ++i)
            {
                var instances = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Application.ExecutablePath));
                if (instances.Length == 1)
                    return true;
                Thread.Sleep(1250);
            }

            return false;
        }

        /// <summary>
        /// Closes other instances of this program, deletes supposed exe location and copies this program
        /// to that location.
        /// </summary>
        private static void ReplaceCurrent()
        {
            CloseOtherProcess();
            File.Delete(StartupLauncher.ExecutablePath);
            File.Copy(Application.ExecutablePath, StartupLauncher.ExecutablePath);
        }

        /// <summary>
        /// Checks if current exe and the supposed exe location match and if file contents are equal.
        /// </summary>
        /// <returns>True if current exe is in another location than supposed exe and has different binary. Otherwise false.</returns>
        private static bool ShouldReplaceCurrent()
        {
            return StartupLauncher.ExecutablePath != Application.ExecutablePath 
                   && !Utils.AreFileContentsEqual(StartupLauncher.ExecutablePath, Application.ExecutablePath);
        }

        private static bool ShouldOpenMainExe() => StartupLauncher.ExecutablePath != Application.ExecutablePath;

        /// <summary>
        /// Closes all other instances of this program.
        /// </summary>
        public static void CloseOtherProcess()
        {
            Console.WriteLine("Closing another process!");
            Process[] runningProcesses = Process.GetProcesses();
            int ownProcessId = Process.GetCurrentProcess().Id;
            foreach (Process process in runningProcesses)
            {
                if (process.ProcessName == "Wellbeing" && process.Id != ownProcessId)
                {
                    Logger.Log("Closing other process.");
                    process.CloseMainWindow();
                    Thread.Sleep(200);
                    process.Kill();
                    process.WaitForExit(2000);
                }
            }
        }

        static void MyHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Console.WriteLine("Unhandled exception:" + e.Message);
            Logger.Log("Global Error: " + e.Message);
        }
    }
}