using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace DellG15FanControl
{
    internal static class Program
    {
        private const string MutexName = "Global\\DellG15LegacyFanControl-5515";
        private static EventWaitHandle watchdogSkipRestore;

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 1 && (args[0] == "--enable-startup" || args[0] == "--disable-startup"))
            {
                try
                {
                    StartupTask.SetEnabled(args[0] == "--enable-startup");
                    Environment.ExitCode = 0;
                }
                catch { Environment.ExitCode = 10; }
                return;
            }

            if (args.Length >= 3 && args[0] == "--watchdog")
            {
                int pid;
                if (Int32.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out pid))
                    RunWatchdog(pid, args[2]);
                return;
            }

            bool created;
            using (Mutex mutex = new Mutex(true, MutexName, out created))
            {
                if (!created)
                {
                    MessageBox.Show("程序已在运行。\nThe application is already running.",
                        "Dell G15 Fan Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool minimized = Array.IndexOf(args, "--startup") >= 0;
                Application.Run(new MainForm(minimized));
            }
        }

        internal static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        internal static void StartWatchdog()
        {
            using (Process current = Process.GetCurrentProcess())
            {
                string eventName = "Local\\DellG15FanControl-WatchdogSkip-" + current.Id.ToString(CultureInfo.InvariantCulture);
                if (watchdogSkipRestore != null) watchdogSkipRestore.Dispose();
                watchdogSkipRestore = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);

                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = Application.ExecutablePath;
                info.Arguments = "--watchdog " + current.Id.ToString(CultureInfo.InvariantCulture) + " " + eventName;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(info);
            }
        }

        internal static void SuppressWatchdogRestore()
        {
            try { if (watchdogSkipRestore != null) watchdogSkipRestore.Set(); }
            catch { }
        }

        private static void RunWatchdog(int parentPid, string skipEventName)
        {
            EventWaitHandle skip = null;
            try { skip = EventWaitHandle.OpenExisting(skipEventName); }
            catch { }

            try
            {
                try
                {
                    Process parent = Process.GetProcessById(parentPid);
                    parent.WaitForExit();
                }
                catch
                {
                }

                if (skip != null && skip.WaitOne(0)) return;

                try
                {
                    PlatformPolicy.DemandExactMatch();
                    using (IDiagsTransport transport = PowerShellCimTransport.Connect())
                    {
                        FanFirmware firmware = new FanFirmware(transport);
                        firmware.VerifyRevision();
                        firmware.SetBoth(FanState.Auto);
                    }
                }
                catch
                {
                }
            }
            finally
            {
                if (skip != null) skip.Dispose();
            }
        }
    }
}
