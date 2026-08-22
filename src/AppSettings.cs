using System;
using System.Globalization;
using System.IO;

namespace DellG15FanControl
{
    internal sealed class AppSettings
    {
        internal string Language = "zh-CN";
        internal int OffMax = 50;
        internal int LowMax = 65;
        internal int HighMax = 80;
        internal bool StartWithWindows;
        internal bool StartMinimized;

        private static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DellG15FanControl", "settings.ini"); }
        }

        internal static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string[] pair = raw.Split(new char[] { '=' }, 2);
                    if (pair.Length != 2) continue;
                    int n; bool b;
                    if (pair[0] == "Language") settings.Language = pair[1] == "en-US" ? "en-US" : "zh-CN";
                    else if (pair[0] == "OffMax" && Int32.TryParse(pair[1], out n)) settings.OffMax = n;
                    else if (pair[0] == "LowMax" && Int32.TryParse(pair[1], out n)) settings.LowMax = n;
                    else if (pair[0] == "HighMax" && Int32.TryParse(pair[1], out n)) settings.HighMax = n;
                    else if (pair[0] == "StartWithWindows" && Boolean.TryParse(pair[1], out b)) settings.StartWithWindows = b;
                    else if (pair[0] == "StartMinimized" && Boolean.TryParse(pair[1], out b)) settings.StartMinimized = b;
                }
            }
            catch { }
            settings.Normalize();
            return settings;
        }

        internal void Save()
        {
            Normalize();
            string directory = Path.GetDirectoryName(FilePath);
            Directory.CreateDirectory(directory);
            File.WriteAllLines(FilePath, new string[] {
                "Language=" + Language,
                "OffMax=" + OffMax.ToString(CultureInfo.InvariantCulture),
                "LowMax=" + LowMax.ToString(CultureInfo.InvariantCulture),
                "HighMax=" + HighMax.ToString(CultureInfo.InvariantCulture),
                "StartWithWindows=" + StartWithWindows.ToString(CultureInfo.InvariantCulture),
                "StartMinimized=" + StartMinimized.ToString(CultureInfo.InvariantCulture)
            });
        }

        private void Normalize()
        {
            if (OffMax < 30 || OffMax > 70) OffMax = 50;
            if (LowMax <= OffMax || LowMax > 85) LowMax = Math.Max(OffMax + 5, 65);
            if (HighMax <= LowMax || HighMax > 95) HighMax = Math.Max(LowMax + 5, 80);
        }
    }
}
