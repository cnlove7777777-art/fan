using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DellG15FanControl
{
    internal sealed class MainForm : Form
    {
        private readonly AppSettings settings;
        private readonly System.Windows.Forms.Timer timer;
        private readonly NotifyIcon tray;
        private readonly bool launchedFromStartup;
        private IDiagsTransport transport;
        private FanFirmware firmware;
        private int busy;
        private volatile bool exiting;
        private volatile bool systemEnding;
        private volatile bool logoffEnding;
        private bool thermalOverride;
        private FanState? selectedManualState;
        private Telemetry lastTelemetry;
        private volatile int emergencyThreshold;
        private Label cpu, gpu, fan0, fan1, status, thresholdText;
        private Button offButton, lowButton, highButton;
        private TrackBar threshold;
        private ComboBox language;
        private Button menuButton;
        private ContextMenuStrip appMenu;
        private ToolStripMenuItem startupMenuItem, exitMenuItem;
        private MenuItem trayStartupItem;
        private bool allowExit;

        internal MainForm(bool launchedMinimized)
        {
            launchedFromStartup = launchedMinimized;
            settings = AppSettings.Load();
            emergencyThreshold = settings.EmergencyThreshold;
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
            trayStartupItem = new MenuItem("Start with Windows / 开机自启动", delegate { ToggleStartup(); });
            tray.ContextMenu = new ContextMenu(new MenuItem[] {
                new MenuItem("Open / 打开", delegate { ShowFromTray(); }),
                trayStartupItem,
                new MenuItem("Exit / 退出", delegate { RequestExit(); }) });
            Shown += delegate {
                try { Program.StartWatchdog(); } catch { }
                RefreshStartupState();
                if (launchedMinimized) BeginInvoke(new Action(HideToTray));
                ConnectAsync();
            };
            Resize += delegate { if (WindowState == FormWindowState.Minimized) HideToTray(); };
            FormClosing += OnClosing;
            SystemEvents.SessionEnding += OnSessionEnding;
        }

        private void InitializeUi()
        {
            Text = "Dell G15 Fan Control";
            ClientSize = new Size(790, 220);
            MinimumSize = new Size(700, 245);
            MaximumSize = new Size(1200, 360);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5F);
            BackColor = Color.FromArgb(246, 248, 251);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            MaximizeBox = false;
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill; root.Padding = new Padding(14, 8, 14, 7);
            root.ColumnCount = 1; root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 43F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 29F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 19F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 9F));
            Controls.Add(root);

            TableLayoutPanel readings = new TableLayoutPanel();
            readings.Dock = DockStyle.Fill; readings.ColumnCount = 4;
            for (int i = 0; i < 4; i++) readings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            cpu = Reading("CPU\n-- °C"); gpu = Reading("GPU\n-- °C");
            fan0 = Reading("Fan 1\n-- RPM"); fan1 = Reading("Fan 2\n-- RPM");
            readings.Controls.Add(cpu, 0, 0); readings.Controls.Add(gpu, 1, 0);
            readings.Controls.Add(fan0, 2, 0); readings.Controls.Add(fan1, 3, 0);
            root.Controls.Add(readings, 0, 0);

            TableLayoutPanel controls = new TableLayoutPanel();
            controls.Dock = DockStyle.Fill; controls.ColumnCount = 6;
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17F));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17F));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17F));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31F));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11F));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 7F));
            offButton = ModeButton(Color.FromArgb(70, 74, 82));
            lowButton = ModeButton(Color.FromArgb(44, 111, 170));
            highButton = ModeButton(Color.FromArgb(197, 76, 49));
            offButton.Click += delegate { SelectManualState(FanState.Off); };
            lowButton.Click += delegate { SelectManualState(FanState.Low); };
            highButton.Click += delegate { SelectManualState(FanState.High); };
            controls.Controls.Add(offButton, 0, 0); controls.Controls.Add(lowButton, 1, 0); controls.Controls.Add(highButton, 2, 0);
            threshold = new TrackBar(); threshold.Dock = DockStyle.Fill;
            threshold.Minimum = 60; threshold.Maximum = 100; threshold.TickFrequency = 5;
            threshold.SmallChange = 1; threshold.LargeChange = 5; threshold.Value = emergencyThreshold;
            threshold.ValueChanged += delegate {
                emergencyThreshold = threshold.Value;
                settings.EmergencyThreshold = emergencyThreshold;
                settings.Save();
                UpdateThresholdText();
            };
            controls.Controls.Add(threshold, 3, 0);
            language = new ComboBox(); language.Dock = DockStyle.Fill; language.DropDownStyle = ComboBoxStyle.DropDownList;
            language.Items.AddRange(new object[] { "中", "EN" }); language.SelectedIndex = settings.Language == "en-US" ? 1 : 0;
            language.SelectedIndexChanged += delegate { settings.Language = language.SelectedIndex == 1 ? "en-US" : "zh-CN"; settings.Save(); ApplyLanguage(); };
            controls.Controls.Add(language, 4, 0);
            menuButton = new Button(); menuButton.Dock = DockStyle.Fill; menuButton.Text = "⋮";
            menuButton.Font = new Font("Segoe UI", 15F, FontStyle.Bold); menuButton.FlatStyle = FlatStyle.Flat;
            menuButton.FlatAppearance.BorderSize = 0; menuButton.Margin = new Padding(4, 5, 0, 5);
            controls.Controls.Add(menuButton, 5, 0); root.Controls.Add(controls, 0, 1);
            appMenu = new ContextMenuStrip();
            startupMenuItem = new ToolStripMenuItem(); startupMenuItem.CheckOnClick = false;
            startupMenuItem.Click += delegate { ToggleStartup(); };
            exitMenuItem = new ToolStripMenuItem(); exitMenuItem.Click += delegate { RequestExit(); };
            appMenu.Items.Add(startupMenuItem); appMenu.Items.Add(new ToolStripSeparator()); appMenu.Items.Add(exitMenuItem);
            appMenu.Opening += delegate { RefreshStartupState(); };
            menuButton.Click += delegate { appMenu.Show(menuButton, new Point(0, menuButton.Height)); };
            thresholdText = new Label(); thresholdText.Dock = DockStyle.Fill; thresholdText.TextAlign = ContentAlignment.MiddleCenter;
            thresholdText.ForeColor = Color.FromArgb(65, 70, 80); root.Controls.Add(thresholdText, 0, 2);
            status = new Label(); status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleLeft;
            status.AutoEllipsis = true; status.ForeColor = Color.FromArgb(80, 85, 95); root.Controls.Add(status, 0, 3);
        }

        private Label Reading(string text)
        {
            Label label = new Label(); label.Dock = DockStyle.Fill; label.Text = text;
            label.TextAlign = ContentAlignment.MiddleCenter; label.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(35, 40, 48); return label;
        }

        private Button ModeButton(Color color)
        {
            Button button = new Button(); button.Dock = DockStyle.Fill; button.Margin = new Padding(4, 5, 4, 5);
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0;
            button.BackColor = color; button.ForeColor = Color.White; return button;
        }

        private bool English { get { return settings.Language == "en-US"; } }
        private string T(string zh, string en) { return English ? en : zh; }

        private void ApplyLanguage()
        {
            offButton.Text = "0 RPM"; lowButton.Text = T("Dell 低速", "Dell Low"); highButton.Text = T("Dell 高速", "Dell High");
            startupMenuItem.Text = T("开机自启动并常驻托盘", "Start with Windows in tray");
            exitMenuItem.Text = T("退出程序并恢复 BIOS 自动", "Exit and restore BIOS Auto");
            UpdateThresholdText();
            if (status.Text.Length == 0) status.Text = T("正在连接 Dell LegacyDiags……", "Connecting to Dell LegacyDiags...");
        }

        private void UpdateThresholdText()
        {
            thresholdText.Text = T("CPU 与 GPU 同时达到 ", "BIOS Auto when both CPU and GPU reach ") +
                emergencyThreshold.ToString(CultureInfo.InvariantCulture) + T(" °C：BIOS 临时接管；降温后恢复所选档",
                " °C; selected mode resumes after cooling");
        }

        private FanState? SavedManualState()
        {
            return settings.LastManualState >= 0 && settings.LastManualState <= 2
                ? (FanState?)((FanState)settings.LastManualState) : null;
        }

        private bool WaitWhileRunning(int milliseconds)
        {
            int remaining = milliseconds;
            while (remaining > 0)
            {
                if (exiting || systemEnding) return false;
                int slice = Math.Min(250, remaining);
                Thread.Sleep(slice);
                remaining -= slice;
            }
            return !exiting && !systemEnding;
        }

        private void PostToUi(Action action)
        {
            if (exiting || systemEnding || IsDisposed || Disposing || !IsHandleCreated) return;
            try { BeginInvoke(action); }
            catch (InvalidOperationException) { }
            catch (ObjectDisposedException) { }
        }

        private void ConnectAsync()
        {
            SetButtons(false);
            ThreadPool.QueueUserWorkItem(delegate {
                Exception lastError = null;
                int attempts = launchedFromStartup ? 4 : 2;

                if (launchedFromStartup && !WaitWhileRunning(10000)) return;

                for (int attempt = 1; attempt <= attempts && !exiting && !systemEnding; attempt++)
                {
                    IDiagsTransport candidate = null;
                    try
                    {
                        PlatformPolicy.DemandExactMatch();
                        candidate = PowerShellCimTransport.Connect();
                        FanFirmware candidateFirmware = new FanFirmware(candidate);
                        candidateFirmware.VerifyRevision();

                        Telemetry initial = candidateFirmware.ReadTelemetry();
                        FanState? restoreState = SavedManualState();
                        bool hot = restoreState.HasValue && IsBothHot(initial);
                        if (restoreState.HasValue)
                        {
                            candidateFirmware.SetBoth(hot ? FanState.Auto : restoreState.Value);
                            initial = candidateFirmware.ReadTelemetry();
                        }

                        if (exiting || systemEnding)
                        {
                            if (!systemEnding) candidate.Dispose();
                            return;
                        }

                        transport = candidate;
                        firmware = candidateFirmware;
                        candidate = null;
                        lastTelemetry = initial;
                        selectedManualState = restoreState;
                        thermalOverride = hot;

                        PostToUi(delegate {
                            if (restoreState.HasValue) SetSelectedButton(restoreState.Value);
                            Display(initial);
                            if (restoreState.HasValue)
                                status.Text = hot
                                    ? T("已恢复上次档位：", "Restored saved mode: ") + StateName(restoreState.Value) + T("；当前由 BIOS 自动接管。", "; BIOS Auto is active while hot.")
                                    : T("已恢复上次档位：", "Restored saved mode: ") + StateName(restoreState.Value);
                            else
                                status.Text = T("已连接；请选择手动档位。", "Connected; select a manual mode.");
                            SetButtons(true);
                            timer.Start();
                            RefreshAsync();
                        });
                        return;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        try { if (candidate != null) candidate.Dispose(); } catch { }
                        if (attempt < attempts && !exiting && !systemEnding)
                        {
                            int shownAttempt = attempt + 1;
                            PostToUi(delegate {
                                status.Text = T("Dell CIM 尚未就绪，正在重试（", "Dell CIM is not ready; retrying (") +
                                    shownAttempt.ToString(CultureInfo.InvariantCulture) + "/" + attempts.ToString(CultureInfo.InvariantCulture) + ")…";
                            });
                            if (!WaitWhileRunning(5000)) return;
                        }
                    }
                }

                if (lastError != null && !exiting && !systemEnding)
                    PostToUi(delegate { status.Text = T("连接失败：", "Connection failed: ") + lastError.Message; });
            });
        }

        private void RefreshAsync()
        {
            if (firmware == null || exiting || systemEnding || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
            ThreadPool.QueueUserWorkItem(delegate {
                try
                {
                    Telemetry value = firmware.ReadTelemetry(); lastTelemetry = value;
                    ApplyThermalOverride(value); PostToUi(delegate { Display(value); });
                }
                catch (Exception ex)
                {
                    if (!exiting && !systemEnding)
                        PostToUi(delegate { status.Text = T("刷新失败：", "Refresh failed: ") + ex.Message; });
                }
                finally { Interlocked.Exchange(ref busy, 0); }
            });
        }

        private void Display(Telemetry value)
        {
            cpu.Text = "CPU\n" + value.CpuC + " °C";
            gpu.Text = "GPU\n" + (value.GpuC.HasValue ? value.GpuC.Value + " °C" : "N/A");
            fan0.Text = T("风扇 1", "Fan 1") + "\n" + value.Fan0.Rpm + " RPM";
            fan1.Text = T("风扇 2", "Fan 2") + "\n" + value.Fan1.Rpm + " RPM";
            if (thermalOverride) status.Text = T("BIOS 自动接管中 · ", "BIOS Auto override · ") + value.Time.ToString("HH:mm:ss");
            else if (selectedManualState.HasValue) status.Text = T("手动档：", "Manual: ") + StateName(selectedManualState.Value) + " · " + value.Time.ToString("HH:mm:ss");
            else status.Text = T("当前：", "Current: ") + StateName(value.Fan0.State) + T("；请选择手动档。", "; select a manual mode.");
        }

        private string StateName(FanState state)
        {
            if (state == FanState.Off) return "0 RPM";
            if (state == FanState.Low) return T("Dell 低速", "Dell Low");
            if (state == FanState.High) return T("Dell 高速", "Dell High");
            return T("BIOS 自动", "BIOS Auto");
        }

        private void SelectManualState(FanState state)
        {
            if (firmware == null || exiting || systemEnding || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
            SetButtons(false);
            ThreadPool.QueueUserWorkItem(delegate {
                try
                {
                    bool hot = IsBothHot(lastTelemetry);
                    firmware.SetBoth(hot ? FanState.Auto : state);
                    selectedManualState = state;
                    thermalOverride = hot;
                    PostToUi(delegate {
                        settings.LastManualState = (int)state;
                        settings.Save();
                        SetSelectedButton(state);
                        status.Text = hot ? T("已记住所选档；当前由 BIOS 自动接管。", "Mode saved; BIOS Auto is active while both sensors are hot.")
                            : T("已切换并记住：", "Applied and saved: ") + StateName(state);
                        SetButtons(true);
                    });
                }
                catch (Exception ex)
                {
                    PostToUi(delegate { status.Text = T("切换失败：", "Switch failed: ") + ex.Message; SetButtons(true); });
                }
                finally { Interlocked.Exchange(ref busy, 0); }
            });
        }

        private void ApplyThermalOverride(Telemetry value)
        {
            if (!selectedManualState.HasValue || !value.GpuC.HasValue || exiting || systemEnding) return;
            bool hot = IsBothHot(value);
            if (hot && !thermalOverride) { firmware.SetBoth(FanState.Auto); thermalOverride = true; }
            else if (!hot && thermalOverride) { firmware.SetBoth(selectedManualState.Value); thermalOverride = false; }
        }

        private bool IsBothHot(Telemetry value)
        {
            int limit = emergencyThreshold;
            return value != null && value.GpuC.HasValue && value.CpuC >= limit && value.GpuC.Value >= limit;
        }

        private void SetSelectedButton(FanState state)
        {
            offButton.FlatAppearance.BorderSize = state == FanState.Off ? 3 : 0;
            lowButton.FlatAppearance.BorderSize = state == FanState.Low ? 3 : 0;
            highButton.FlatAppearance.BorderSize = state == FanState.High ? 3 : 0;
            offButton.FlatAppearance.BorderColor = lowButton.FlatAppearance.BorderColor = highButton.FlatAppearance.BorderColor = Color.Gold;
        }

        private void SetButtons(bool enabled) { offButton.Enabled = lowButton.Enabled = highButton.Enabled = enabled; }
        private void HideToTray() { Hide(); ShowInTaskbar = false; }
        private void ShowFromTray() { Show(); ShowInTaskbar = true; WindowState = FormWindowState.Normal; Activate(); }

        private void RefreshStartupState()
        {
            string reason;
            bool enabled = StartupTask.VerifyExact(out reason);
            startupMenuItem.Checked = enabled;
            trayStartupItem.Checked = enabled;
            settings.StartWithWindows = enabled;
            settings.Save();
        }

        private void ToggleStartup()
        {
            try
            {
                string reason;
                bool current = StartupTask.VerifyExact(out reason);
                StartupTask.SetEnabled(!current);
                bool verified = StartupTask.VerifyExact(out reason);
                startupMenuItem.Checked = verified; trayStartupItem.Checked = verified;
                settings.StartWithWindows = verified; settings.Save();
                status.Text = verified ? T("已验证：开机后将以最高权限常驻托盘。", "Verified: starts at logon with highest privileges in the tray.")
                    : T("已验证：开机自启动已取消。", "Verified: startup task removed.");
            }
            catch (Exception ex)
            {
                RefreshStartupState();
                MessageBox.Show(T("开机启动设置失败。请确认程序以管理员身份运行。\n\n", "Startup setup failed. Verify that the app is running as administrator.\n\n") + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RequestExit()
        {
            allowExit = true;
            ShowFromTray();
            Close();
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            if (e.Reason == SessionEndReasons.SystemShutdown)
            {
                systemEnding = true;
                Program.SuppressWatchdogRestore();
            }
            else
                logoffEnding = true;
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            bool windowsShutdown = systemEnding || (e.CloseReason == CloseReason.WindowsShutDown && !logoffEnding);
            if (!allowExit && !windowsShutdown && !logoffEnding && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                status.Text = T("已最小化到托盘；风扇控制继续运行。", "Minimized to tray; fan control remains active.");
                return;
            }

            if (windowsShutdown)
            {
                systemEnding = true;
                exiting = true;
                Program.SuppressWatchdogRestore();
                try { timer.Stop(); } catch { }
                try { SystemEvents.SessionEnding -= OnSessionEnding; } catch { }
                try { tray.Visible = false; tray.Dispose(); } catch { }
                PowerShellCimTransport cim = transport as PowerShellCimTransport;
                if (cim != null) cim.Abort();
                return;
            }

            if (exiting) return;
            exiting = true;
            timer.Stop();
            bool restored = true;
            try { if (firmware != null) firmware.SetBoth(FanState.Auto); }
            catch (Exception ex)
            {
                restored = false;
                if (!logoffEnding)
                {
                    DialogResult result = MessageBox.Show(T("恢复 BIOS 自动失败：", "Failed to restore BIOS Auto: ") + ex.Message + "\n" +
                        T("仍要退出吗？看门狗还会重试。", "Exit anyway? The watchdog will retry."), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                    if (result == DialogResult.No)
                    {
                        e.Cancel = true;
                        exiting = false;
                        timer.Start();
                        return;
                    }
                }
            }

            if (restored) Program.SuppressWatchdogRestore();
            try { SystemEvents.SessionEnding -= OnSessionEnding; } catch { }
            tray.Visible = false;
            tray.Dispose();
            if (transport != null) transport.Dispose();
        }
    }
}
