using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

internal static class UiSmoke
{
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
            if (form.Text != "Dell G15 Fan Control" || form.Controls.Count < 10 ||
                form.ClientSize.Width < 600 || form.ClientSize.Height < 400)
                return 4;
            Console.WriteLine("UI smoke test: PASS ({0} top-level controls)", form.Controls.Count);
            return 0;
        }
        finally { form.Dispose(); }
    }
}
