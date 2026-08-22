using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

internal static class UiSmoke
{
    private static int CountControls(Control root)
    {
        int count = root.Controls.Count;
        foreach (Control child in root.Controls) count += CountControls(child);
        return count;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1) return 2;
        Assembly assembly = Assembly.LoadFile(args[0]);
        Type type = assembly.GetType("DellG15FanControl.MainForm", true);
        object instance = Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { false }, CultureInfo.InvariantCulture);
        Form form = instance as Form;
        if (form == null) return 3;
        try
        {
            int controls = CountControls(form);
            if (form.Text != "Dell G15 Fan Control" || controls < 12 ||
                form.ClientSize.Width < 650 || form.ClientSize.Height < 180 ||
                form.ClientSize.Height > 280)
                return 4;
            Console.WriteLine("UI smoke test: PASS ({0} controls)", controls);
            return 0;
        }
        finally { form.Dispose(); }
    }
}
