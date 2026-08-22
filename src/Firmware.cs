using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management;
using Microsoft.Win32;

namespace DellG15FanControl
{
    internal enum FanState { Off = 0, Low = 1, High = 2, Auto = 3 }

    internal sealed class FanReading
    {
        internal int Index;
        internal FanState State;
        internal int Rpm;
    }

    internal sealed class Telemetry
    {
        internal FanReading Fan0;
        internal FanReading Fan1;
        internal int CpuC;
        internal int? GpuC;
        internal DateTime Time;
    }

    internal sealed class Registers
    {
        internal uint Eax, Ebx, Ecx, Edx;
    }

    internal static class PlatformPolicy
    {
        internal const string Manufacturer = "Dell Inc.";
        internal const string Model = "Dell G15 5515";
        internal const string Bios = "1.30.0";

        internal static void DemandExactMatch()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\BIOS", false))
            {
                if (key == null) throw new InvalidOperationException("BIOS identity is unavailable.");
                string maker = Convert.ToString(key.GetValue("SystemManufacturer"), CultureInfo.InvariantCulture).Trim();
                string model = Convert.ToString(key.GetValue("SystemProductName"), CultureInfo.InvariantCulture).Trim();
                string bios = Convert.ToString(key.GetValue("BIOSVersion"), CultureInfo.InvariantCulture).Trim();
                if (!String.Equals(maker, Manufacturer, StringComparison.OrdinalIgnoreCase) ||
                    !String.Equals(model, Model, StringComparison.OrdinalIgnoreCase) ||
                    !String.Equals(bios, Bios, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Unsupported platform: " + maker + " / " + model + " / BIOS " + bios);
            }
        }
    }

    internal sealed class LegacyDiagsTransport : IDisposable
    {
        private const string NamespacePath = @"\\.\root\dcim\sysman\diagnostics";
        private static readonly HashSet<uint> Allowed = new HashSet<uint>(new uint[] {
            0x000000A3, 0x000001A3, 0x000002A3, 0x000004A3,
            0x000010A3, 0x000011A3, 0x0000FEA3 });
        private readonly object gate = new object();
        private ManagementObject instance;

        private LegacyDiagsTransport(ManagementObject value) { instance = value; }

        internal static LegacyDiagsTransport Connect()
        {
            ManagementScope scope = new ManagementScope(NamespacePath);
            scope.Connect();
            ManagementObject selected = null;
            int count = 0;
            using (ManagementObjectSearcher search = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT * FROM LegacyDiags")))
            using (ManagementObjectCollection results = search.Get())
            {
                foreach (ManagementObject item in results)
                {
                    try
                    {
                        object active = item["Active"];
                        if (!(active is bool) || !(bool)active) continue;
                        count++;
                        if (selected == null)
                        {
                            selected = new ManagementObject(scope, new ManagementPath(item.Path.Path), null);
                            selected.Get();
                        }
                    }
                    finally { item.Dispose(); }
                }
            }
            if (count != 1 || selected == null)
            {
                if (selected != null) selected.Dispose();
                throw new InvalidOperationException("Expected one active Dell LegacyDiags provider; found " + count + ".");
            }
            return new LegacyDiagsTransport(selected);
        }

        internal Registers Execute(uint eax, uint ebx, uint ecx, uint edx)
        {
            if (!Allowed.Contains(eax)) throw new InvalidOperationException("Command is not allowlisted.");
            lock (gate)
            {
                using (ManagementBaseObject input = instance.GetMethodParameters("Execute"))
                {
                    Put(input, "Eax", eax); Put(input, "Ebx", ebx);
                    Put(input, "Ecx", ecx); Put(input, "Edx", edx);
                    InvokeMethodOptions options = new InvokeMethodOptions();
                    options.Timeout = TimeSpan.FromSeconds(4);
                    using (ManagementBaseObject output = instance.InvokeMethod("Execute", input, options))
                    {
                        if (output == null || !(output["ReturnValue"] is bool) || !(bool)output["ReturnValue"])
                            throw new InvalidOperationException("LegacyDiags.Execute failed.");
                        return new Registers { Eax = Get(output, "Eax"), Ebx = Get(output, "Ebx"),
                            Ecx = Get(output, "Ecx"), Edx = Get(output, "Edx") };
                    }
                }
            }
        }

        private static void Put(ManagementBaseObject value, string name, uint register)
        {
            value[name + "Len"] = (uint)4;
            value[name + "Val"] = BitConverter.GetBytes(register);
        }

        private static uint Get(ManagementBaseObject value, string name)
        {
            byte[] bytes = value[name + "Val"] as byte[];
            uint length = Convert.ToUInt32(value[name + "Len"], CultureInfo.InvariantCulture);
            if (length != 4 || bytes == null || bytes.Length != 4)
                throw new InvalidOperationException("Invalid register response: " + name);
            return BitConverter.ToUInt32(bytes, 0);
        }

        public void Dispose() { if (instance != null) { instance.Dispose(); instance = null; } }
    }

    internal sealed class FanFirmware
    {
        private readonly LegacyDiagsTransport transport;
        private bool verified;
        internal FanFirmware(LegacyDiagsTransport value) { transport = value; }

        internal int VerifyRevision()
        {
            Registers r = transport.Execute(0xFEA3, 0, 0x20, 0);
            if (r.Eax != 0x44494147 || r.Edx != 0x44454C4C)
                throw new InvalidOperationException("Dell DIAG/DELL signature check failed.");
            verified = true;
            return (int)(r.Ecx & 0xFFFF);
        }

        private void DemandVerified() { if (!verified) throw new InvalidOperationException("Revision check required."); }
        private static uint Pack(int fan, FanState state) { return (uint)(fan | ((int)state << 8)); }

        internal FanReading ReadFan(int fan)
        {
            DemandVerified();
            int state = ReadWord(0x00A3, (uint)fan);
            if (state < 0 || state > 3) throw new InvalidOperationException("Unknown fan state.");
            return new FanReading { Index = fan, State = (FanState)state, Rpm = ReadWord(0x02A3, (uint)fan) };
        }

        internal int ReadTemperature(int sensor)
        {
            DemandVerified();
            int value = ReadWord(0x10A3, (uint)sensor);
            if (value < 0 || value > 127) throw new InvalidOperationException("Invalid temperature.");
            return value;
        }

        internal void SetFan(int fan, FanState state)
        {
            DemandVerified();
            Registers r = transport.Execute(0x01A3, Pack(fan, state), 0, 0);
            if ((r.Eax & 0xFFFF) == 0xFFFF) throw new InvalidOperationException("Firmware rejected fan state.");
        }

        internal void SetBoth(FanState state)
        {
            Exception first = null;
            try { SetFan(0, state); } catch (Exception ex) { first = ex; }
            try { SetFan(1, state); } catch { if (first == null) throw; }
            if (first != null) throw first;
        }

        internal Telemetry ReadTelemetry()
        {
            return new Telemetry { Fan0 = ReadFan(0), Fan1 = ReadFan(1),
                CpuC = ReadTemperature(0), GpuC = NvidiaTemperature.TryRead(), Time = DateTime.Now };
        }

        private int ReadWord(uint command, uint ebx)
        {
            int value = (int)(transport.Execute(command, ebx, 0, 0).Eax & 0xFFFF);
            if (value == 0xFFFF) throw new InvalidOperationException("Firmware returned FFFF.");
            return value;
        }
    }

    internal static class NvidiaTemperature
    {
        internal static int? TryRead()
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe");
                if (!File.Exists(path)) return null;
                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo();
                info.FileName = path;
                info.Arguments = "--query-gpu=temperature.gpu --format=csv,noheader,nounits";
                info.UseShellExecute = false; info.CreateNoWindow = true;
                info.RedirectStandardOutput = true; info.RedirectStandardError = true;
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(info))
                {
                    if (!process.WaitForExit(2000)) { try { process.Kill(); } catch { } return null; }
                    string text = process.StandardOutput.ReadToEnd().Trim();
                    int value;
                    if (process.ExitCode == 0 && Int32.TryParse(text, NumberStyles.None,
                        CultureInfo.InvariantCulture, out value) && value > 0 && value < 121) return value;
                }
            }
            catch { }
            return null;
        }
    }
}
