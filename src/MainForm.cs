using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace DellG15FanControl
{
    internal sealed class MainForm : Form
    {
        private readonly AppSettings settings;
        private readonly System.Windows.Forms.Timer timer;
        private readonly NotifyIcon tray;
        private LegacyDiagsTransport transport;
        private FanFirmware firmware;
        private int busy;
        private bool exiting;
        private bool curveEnabled;
        private bool updatingStartup;
        private FanState? curveState;

        private Label title, platform, cpuTemp, gpuTemp, fan0Rpm, fan1Rpm, fan0State, fan1State;
        private Label status, modeLabel, curveLabel, startupLabel, languageLabel;
        private Button autoButton, offButton, lowButton, highButton, curveButton;
        private NumericUpDown offMax, lowMax, highMax;
        private CheckBox startup, startMinimized;
        private ComboBox language;

        internal MainForm(bool launchedMinimized)
        {
            settings = AppSettings.Load();
            InitializeUi();
            ApplyLanguage();
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1200;
            timer.Tick += delegate { RefreshAsync(); };
            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Information;
            tray.Text = "Dell G15 Fan Control";
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowFromTray(); };
            tray.ContextMenu = new ContextMenu(new MenuItem[] {
                new MenuItem("Open / 打开", delegate { ShowFromTray(); }),
                new MenuItem("Exit / 退出", delegate { Close(); }) });

            Shown += delegate
            {
                ConnectAsync();
                try { Program.StartWatchdog(); } catch { }
                if (launchedMinimized && settings.StartMinimized) BeginInvoke(new Action(HideToTray));
            };
            Resize += delegate { if (WindowState == FormWindowState.Minimized) HideToTray(); };
            FormClosing += OnClosing;
        }

        private void InitializeUi()
        {
            Text = "Dell G15 Fan Control";
            ClientSize = new Size(720, 510);
            MinimumSize = new Size(700, 520);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(246, 248, 251);

            title = NewLabel(18F, FontStyle.Bold); title.Location = new Point(24, 18); title.AutoSize = true;
            platform = NewLabel(9F, FontStyle.Regular); platform.Location = new Point(27, 55); platform.AutoSize = true;

            GroupBox monitor = new GroupBox(); monitor.Location = new Point(24, 86); monitor.Size = new Size(672, 142);
            monitor.Text = "Monitor"; Controls.Add(monitor);
            cpuTemp = ValueLabel(); cpuTemp.Location = new Point(28, 34);
            gpuTemp = ValueLabel(); gpuTemp.Location = new Point(188, 34);
            fan0Rpm = ValueLabel(); fan0Rpm.Location = new Point(348, 34);
            fan1Rpm = ValueLabel(); fan1Rpm.Location = new Point(508, 34);
            fan0State = NewLabel(9F, FontStyle.Regular); fan0State.Location = new Point(348, 93); fan0State.AutoSize = true;
            fan1State = NewLabel(9F, FontStyle.Regular); fan1State.Location = new Point(508, 93); fan1State.AutoSize = true;
            monitor.Controls.AddRange(new Control[] { cpuTemp, gpuTemp, fan0Rpm, fan1Rpm, fan0State, fan1State });

            modeLabel = NewLabel(10F, FontStyle.Bold); modeLabel.Location = new Point(26, 246); modeLabel.AutoSize = true; Controls.Add(modeLabel);
            autoButton = ModeButton(24, 276, Color.FromArgb(38, 126, 78));
            offButton = ModeButton(158, 276, Color.FromArgb(70, 74, 82));
            lowButton = ModeButton(292, 276, Color.FromArgb(44, 111, 170));
            highButton = ModeButton(426, 276, Color.FromArgb(197, 76, 49));
            curveButton = ModeButton(560, 276, Color.FromArgb(121, 78, 171));
            autoButton.Click += delegate { ApplyStateAsync(FanState.Auto); };
            offButton.Click += delegate { ConfirmOff(); };
            lowButton.Click += delegate { ApplyStateAsync(FanState.Low); };
            highButton.Click += delegate { ApplyStateAsync(FanState.High); };
            curveButton.Click += delegate { ToggleCurve(); };

            curveLabel = NewLabel(9F, FontStyle.Regular); curveLabel.Location = new Point(26, 340); curveLabel.AutoSize = true; Controls.Add(curveLabel);
            offMax = Threshold(240, 335, settings.OffMax); lowMax = Threshold(340, 335, settings.LowMax); highMax = Threshold(440, 335, settings.HighMax);
            Controls.AddRange(new Control[] { offMax, lowMax, highMax });
            Label marks = NewLabel(9F, FontStyle.Regular); marks.Location = new Point(540, 340); marks.AutoSize = true; marks.Text = "°C"; Controls.Add(marks);

            startupLabel = NewLabel(10F, FontStyle.Bold); startupLabel.Location = new Point(26, 385); startupLabel.AutoSize = true; Controls.Add(startupLabel);
            startup = new CheckBox(); startup.Location = new Point(185, 383); startup.AutoSize = true; startup.Checked = settings.StartWithWindows; Controls.Add(startup);
            startMinimized = new CheckBox(); startMinimized.Location = new Point(390, 383); startMinimized.AutoSize = true; startMinimized.Checked = settings.StartMinimized; Controls.Add(startMinimized);
            startup.CheckedChanged += delegate { UpdateStartup(); };
            startMinimized.CheckedChanged += delegate { settings.StartMinimized = startMinimized.Checked; settings.Save(); };

            languageLabel = NewLabel(9F, FontStyle.Regular); languageLabel.Location = new Point(26, 427); languageLabel.AutoSize = true; Controls.Add(languageLabel);
            language = new ComboBox(); language.DropDownStyle = ComboBoxStyle.DropDownList; language.Location = new Point(100, 423); language.Width = 155;
            language.Items.AddRange(new object[] { "中文", "English" }); language.SelectedIndex = settings.Language == "en-US" ? 1 : 0;
            language.SelectedIndexChanged += delegate { settings.Language = language.SelectedIndex == 1 ? "en-US" : "zh-CN"; settings.Save(); ApplyLanguage(); };
            Controls.Add(language);

            status = NewLabel(9F, FontStyle.Regular); status.Location = new Point(26, 466); status.Size = new Size(665, 30); status.ForeColor = Color.FromArgb(80, 85, 95); Controls.Add(status);
            Controls.Add(title); Controls.Add(platform);
        }

        private Label NewLabel(float size, FontStyle style)
        {
            Label value = new Label(); value.Font = new Font("Segoe UI", size, style); value.ForeColor = Color.FromArgb(35, 40, 48); return value;
        }
        private Label ValueLabel() { Label value = NewLabel(18F, FontStyle.Bold); value.Size = new Size(145, 60); return value; }
        private Button ModeButton(int x, int y, Color color)
        {
            Button button = new Button(); button.Location = new Point(x, y); button.Size = new Size(116, 44);
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0; button.BackColor = color; button.ForeColor = Color.White;
            Controls.Add(button); return button;
        }
        private NumericUpDown Threshold(int x, int y, int value)
        {
            NumericUpDown n = new NumericUpDown(); n.Location = new Point(x, y); n.Size = new Size(72, 30); n.Minimum = 30; n.Maximum = 95; n.Value = value;
            n.ValueChanged += delegate { SaveThresholds(); }; return n;
        }

        private bool English { get { return settings.Language == "en-US"; } }
        private string T(string zh, string en) { return English ? en : zh; }

        private void ApplyLanguage()
        {
            title.Text = T("Dell G15 5515 真风扇控制", "Dell G15 5515 True Fan Control");
            platform.Text = T("Dell 官方 LegacyDiags 通道 · BIOS 1.30.0", "Dell LegacyDiags channel · BIOS 1.30.0");
            modeLabel.Text = T("风扇档位（同时控制两只风扇）", "Fan mode (controls both fans)");
            autoButton.Text = T("自动", "Auto"); offButton.Text = T("停转", "Off"); lowButton.Text = T("低速", "Low");
            highButton.Text = T("高速", "High"); curveButton.Text = curveEnabled ? T("曲线：开", "Curve: ON") : T("曲线", "Curve");
            curveLabel.Text = T("曲线阈值： 停转≤       低速≤       高速≤       其余自动", "Curve: Off≤          Low≤          High≤          else Auto");
            startupLabel.Text = T("系统集成", "System integration"); startup.Text = T("开机自启动", "Start with Windows");
            startMinimized.Text = T("启动后最小化", "Start minimized"); languageLabel.Text = T("语言", "Language");
            if (status.Text.Length == 0) status.Text = T("正在连接……", "Connecting...");
        }

        private void ConnectAsync()
        {
            SetButtons(false);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    PlatformPolicy.DemandExactMatch();
                    transport = LegacyDiagsTransport.Connect();
                    firmware = new FanFirmware(transport);
                    int revision = firmware.VerifyRevision();
                    BeginInvoke(new Action(delegate
                    {
                        status.Text = T("已连接。协议版本：", "Connected. Protocol revision: ") + revision.ToString(CultureInfo.InvariantCulture);
                        SetButtons(true); timer.Start(); RefreshAsync();
                    }));
                }
                catch (Exception ex) { BeginInvoke(new Action(delegate { status.Text = T("连接失败：", "Connection failed: ") + ex.Message; })); }
            });
        }

        private void RefreshAsync()
        {
            if (firmware == null || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Telemetry value = firmware.ReadTelemetry();
                    if (curveEnabled) EvaluateCurve(value);
                    BeginInvoke(new Action(delegate { Display(value); }));
                }
                catch (Exception ex) { if (!exiting) BeginInvoke(new Action(delegate { status.Text = T("刷新失败：", "Refresh failed: ") + ex.Message; })); }
                finally { Interlocked.Exchange(ref busy, 0); }
            });
        }

        private void Display(Telemetry value)
        {
            cpuTemp.Text = "CPU\n" + value.CpuC + " °C";
            gpuTemp.Text = "GPU\n" + (value.GpuC.HasValue ? value.GpuC.Value + " °C" : "N/A");
            fan0Rpm.Text = T("风扇 1\n", "Fan 1\n") + value.Fan0.Rpm + " RPM";
            fan1Rpm.Text = T("风扇 2\n", "Fan 2\n") + value.Fan1.Rpm + " RPM";
            fan0State.Text = T("档位：", "State: ") + StateName(value.Fan0.State);
            fan1State.Text = T("档位：", "State: ") + StateName(value.Fan1.State);
            status.Text = T("最后刷新：", "Last refresh: ") + value.Time.ToString("HH:mm:ss") + (curveEnabled ? T(" · 曲线控制中", " · Curve active") : "");
        }

        private string StateName(FanState state)
        {
            if (state == FanState.Off) return T("停转", "Off"); if (state == FanState.Low) return T("低速", "Low");
            if (state == FanState.High) return T("高速", "High"); return T("自动", "Auto");
        }

        private void ConfirmOff()
        {
            DialogResult result = MessageBox.Show(T("将两只内置风扇设为 0 RPM。关闭程序会恢复自动。继续吗？",
                "Set both internal fans to 0 RPM. Closing the app restores Auto. Continue?"), Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes) ApplyStateAsync(FanState.Off);
        }

        private void ApplyStateAsync(FanState state)
        {
            curveEnabled = false; curveState = null; ApplyLanguage();
            if (firmware == null || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
            SetButtons(false); status.Text = T("正在切换……", "Switching...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    firmware.SetBoth(state);
                    BeginInvoke(new Action(delegate { status.Text = T("已切换到：", "Mode applied: ") + StateName(state); SetButtons(true); }));
                }
                catch (Exception ex) { BeginInvoke(new Action(delegate { status.Text = T("切换失败：", "Switch failed: ") + ex.Message; SetButtons(true); })); }
                finally { Interlocked.Exchange(ref busy, 0); }
            });
        }

        private void ToggleCurve()
        {
            SaveThresholds(); curveEnabled = !curveEnabled; curveState = null; ApplyLanguage();
            status.Text = curveEnabled ? T("曲线已启用。", "Curve enabled.") : T("曲线已停止；保留当前档位。", "Curve stopped; current state retained.");
        }

        private void EvaluateCurve(Telemetry value)
        {
            int temp = value.GpuC.HasValue ? Math.Max(value.CpuC, value.GpuC.Value) : value.CpuC;
            FanState desired = temp <= settings.OffMax ? FanState.Off : temp <= settings.LowMax ? FanState.Low : temp <= settings.HighMax ? FanState.High : FanState.Auto;
            if (!curveState.HasValue || curveState.Value != desired)
            {
                firmware.SetBoth(desired);
                curveState = desired;
            }
        }

        private void SaveThresholds()
        {
            if (offMax == null) return;
            int a = (int)offMax.Value, b = (int)lowMax.Value, c = (int)highMax.Value;
            if (!(a < b && b < c)) { status.Text = T("阈值必须依次升高。", "Thresholds must increase."); return; }
            settings.OffMax = a; settings.LowMax = b; settings.HighMax = c; settings.Save();
        }

        private void UpdateStartup()
        {
            if (!IsHandleCreated || updatingStartup) return;
            try
            {
                StartupTask.SetEnabled(startup.Checked);
                settings.StartWithWindows = startup.Checked; settings.Save();
                status.Text = startup.Checked ? T("已设置开机自启动。", "Startup enabled.") : T("已取消开机自启动。", "Startup disabled.");
            }
            catch (Exception ex)
            {
                updatingStartup = true;
                startup.Checked = !startup.Checked;
                updatingStartup = false;
                status.Text = T("启动项设置失败：", "Startup setting failed: ") + ex.Message;
            }
        }

        private void SetButtons(bool enabled)
        {
            autoButton.Enabled = offButton.Enabled = lowButton.Enabled = highButton.Enabled = curveButton.Enabled = enabled;
        }
        private void HideToTray() { Hide(); ShowInTaskbar = false; }
        private void ShowFromTray() { Show(); ShowInTaskbar = true; WindowState = FormWindowState.Normal; Activate(); }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (exiting) return;
            exiting = true; timer.Stop(); curveEnabled = false; status.Text = T("正在恢复 BIOS 自动模式……", "Restoring BIOS Auto...");
            try { if (firmware != null) firmware.SetBoth(FanState.Auto); }
            catch (Exception ex)
            {
                DialogResult result = MessageBox.Show(T("恢复自动模式失败：", "Failed to restore Auto: ") + ex.Message + "\n" +
                    T("仍要退出吗？后台看门狗还会再尝试一次。", "Exit anyway? The watchdog will try once more."), Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                if (result == DialogResult.No) { e.Cancel = true; exiting = false; timer.Start(); return; }
            }
            tray.Visible = false; tray.Dispose(); if (transport != null) transport.Dispose();
        }
    }

    internal static class StartupTask
    {
        private const string TaskName = "DellG15LegacyFanControl";
        internal static void SetEnabled(bool enabled)
        {
            string tool = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
            string args = enabled
                ? "/Create /F /SC ONLOGON /RL HIGHEST /TN \"" + TaskName + "\" /TR \"\\\"" + Application.ExecutablePath + "\\\" --startup\""
                : "/Delete /F /TN \"" + TaskName + "\"";
            ProcessStartInfo info = new ProcessStartInfo(tool, args); info.UseShellExecute = false; info.CreateNoWindow = true;
            using (Process process = Process.Start(info))
            {
                if (!process.WaitForExit(10000) || process.ExitCode != 0)
                    throw new InvalidOperationException("schtasks.exe failed.");
            }
        }
    }
}
