using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace DellG15FanControl
{
    internal static class StartupTask
    {
        internal const string Name = "DellG15LegacyFanControl";

        internal static void SetEnabled(bool enabled)
        {
            if (!Program.IsAdministrator())
                throw new InvalidOperationException("Administrator rights are required.");

            string arguments;
            if (enabled)
            {
                string temporaryXml = Path.Combine(Path.GetTempPath(), "DellG15FanControl-" + Guid.NewGuid().ToString("N") + ".xml");
                try
                {
                    File.WriteAllText(temporaryXml, BuildTaskXml(), Encoding.Unicode);
                    ProcessResult create = Run("/Create /F /TN " + Quote(Name) + " /XML " + Quote(temporaryXml));
                    if (create.ExitCode != 0)
                        throw new InvalidOperationException("schtasks.exe failed (" + create.ExitCode.ToString(CultureInfo.InvariantCulture) + "): " + create.Error.Trim());
                }
                finally
                {
                    try { if (File.Exists(temporaryXml)) File.Delete(temporaryXml); } catch { }
                }
                arguments = null;
            }
            else
                arguments = "/Delete /F /TN " + Quote(Name);

            if (arguments != null)
            {
                ProcessResult result = Run(arguments);
                if (result.ExitCode != 0 && (enabled || Exists()))
                    throw new InvalidOperationException("schtasks.exe failed (" + result.ExitCode.ToString(CultureInfo.InvariantCulture) + "): " + result.Error.Trim());
            }

            string reason;
            bool verified = VerifyExact(out reason);
            if (enabled != verified)
                throw new InvalidOperationException("Startup verification failed: " + reason);
        }

        internal static bool VerifyExact(out string reason)
        {
            ProcessResult result = Run("/Query /TN " + Quote(Name) + " /XML");
            if (result.ExitCode != 0)
            {
                reason = "task is absent";
                return false;
            }

            try
            {
                XmlDocument xml = new XmlDocument();
                xml.PreserveWhitespace = false;
                xml.LoadXml(result.Output);
                string command = NodeText(xml, "//*[local-name()='Actions']/*[local-name()='Exec']/*[local-name()='Command']");
                string args = NodeText(xml, "//*[local-name()='Actions']/*[local-name()='Exec']/*[local-name()='Arguments']");
                string enabled = NodeText(xml, "//*[local-name()='Settings']/*[local-name()='Enabled']");
                string runLevel = NodeText(xml, "//*[local-name()='Principals']/*[local-name()='Principal']/*[local-name()='RunLevel']");
                XmlNode logon = xml.SelectSingleNode("//*[local-name()='Triggers']/*[local-name()='LogonTrigger']");
                command = command == null ? "" : command.Trim().Trim('"');
                if (!String.Equals(Path.GetFullPath(command), Path.GetFullPath(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase))
                { reason = "EXE path does not match"; return false; }
                if (!String.Equals(args == null ? "" : args.Trim(), "--startup", StringComparison.Ordinal))
                { reason = "startup argument does not match"; return false; }
                if (String.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase))
                { reason = "task is disabled"; return false; }
                if (!String.Equals(runLevel, "HighestAvailable", StringComparison.OrdinalIgnoreCase))
                { reason = "task is not highest privilege"; return false; }
                if (logon == null) { reason = "logon trigger is missing"; return false; }
                reason = "verified";
                return true;
            }
            catch (Exception ex)
            {
                reason = "invalid task XML: " + ex.Message;
                return false;
            }
        }

        private static bool Exists()
        {
            return Run("/Query /TN " + Quote(Name)).ExitCode == 0;
        }

        private static string BuildTaskXml()
        {
            string ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            StringBuilder text = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true; settings.OmitXmlDeclaration = false; settings.Encoding = Encoding.Unicode;
            using (XmlWriter writer = XmlWriter.Create(text, settings))
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Task", ns); writer.WriteAttributeString("version", "1.4");
                writer.WriteStartElement("RegistrationInfo", ns);
                writer.WriteElementString("Date", ns, DateTime.Now.ToString("s", CultureInfo.InvariantCulture));
                writer.WriteElementString("Author", ns, identity.Name);
                writer.WriteEndElement();
                writer.WriteStartElement("Principals", ns); writer.WriteStartElement("Principal", ns); writer.WriteAttributeString("id", "Author");
                writer.WriteElementString("UserId", ns, identity.User.Value);
                writer.WriteElementString("LogonType", ns, "InteractiveToken");
                writer.WriteElementString("RunLevel", ns, "HighestAvailable");
                writer.WriteEndElement(); writer.WriteEndElement();
                writer.WriteStartElement("Triggers", ns); writer.WriteStartElement("LogonTrigger", ns);
                writer.WriteElementString("Enabled", ns, "true"); writer.WriteEndElement(); writer.WriteEndElement();
                writer.WriteStartElement("Settings", ns);
                writer.WriteElementString("MultipleInstancesPolicy", ns, "IgnoreNew");
                writer.WriteElementString("DisallowStartIfOnBatteries", ns, "false");
                writer.WriteElementString("StopIfGoingOnBatteries", ns, "false");
                writer.WriteElementString("AllowHardTerminate", ns, "true");
                writer.WriteElementString("StartWhenAvailable", ns, "true");
                writer.WriteElementString("RunOnlyIfNetworkAvailable", ns, "false");
                writer.WriteElementString("AllowStartOnDemand", ns, "true");
                writer.WriteElementString("Enabled", ns, "true");
                writer.WriteElementString("Hidden", ns, "false");
                writer.WriteElementString("RunOnlyIfIdle", ns, "false");
                writer.WriteElementString("WakeToRun", ns, "false");
                writer.WriteElementString("ExecutionTimeLimit", ns, "PT0S");
                writer.WriteElementString("Priority", ns, "7");
                writer.WriteEndElement();
                writer.WriteStartElement("Actions", ns); writer.WriteAttributeString("Context", "Author");
                writer.WriteStartElement("Exec", ns);
                writer.WriteElementString("Command", ns, Application.ExecutablePath);
                writer.WriteElementString("Arguments", ns, "--startup");
                writer.WriteEndElement(); writer.WriteEndElement();
                writer.WriteEndElement(); writer.WriteEndDocument(); writer.Flush();
            }
            return text.ToString();
        }

        private static string NodeText(XmlDocument xml, string xpath)
        {
            XmlNode node = xml.SelectSingleNode(xpath);
            return node == null ? null : node.InnerText;
        }

        private static ProcessResult Run(string arguments)
        {
            string tool = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
            ProcessStartInfo info = new ProcessStartInfo(tool, arguments);
            info.UseShellExecute = false; info.CreateNoWindow = true;
            info.RedirectStandardOutput = true; info.RedirectStandardError = true;
            using (Process process = Process.Start(info))
            {
                if (process == null) throw new InvalidOperationException("schtasks.exe did not start.");
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(10000))
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException("schtasks.exe timed out.");
                }
                return new ProcessResult(process.ExitCode, output, error);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private sealed class ProcessResult
        {
            internal readonly int ExitCode;
            internal readonly string Output;
            internal readonly string Error;
            internal ProcessResult(int exitCode, string output, string error)
            { ExitCode = exitCode; Output = output ?? ""; Error = error ?? ""; }
        }
    }
}
