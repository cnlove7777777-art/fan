# Dell G15 5515 True Fan Control

A compact bilingual Windows utility that gives the **Dell G15 5515** three real manual firmware modes: **Off (0 RPM), Dell Low, and Dell High**, with a temperature-triggered BIOS Auto override.

Unlike AWCC-style “fan boost” sliders, this project calls Dell's existing `LegacyDiags` WMI/SMM interface. On the validated machine, `Off` genuinely stops both internal fans and closing the program immediately restores BIOS automatic control.

> First public release is intentionally restricted to **Dell G15 5515 + BIOS 1.30.0**. Do not remove the model/BIOS allowlist unless you have independently validated the same commands and recovery behavior.

## Features

- Real-time CPU temperature, NVIDIA GPU temperature, fan state and RPM
- Three manual firmware states: true 0 RPM / Dell Low / Dell High
- Adjustable 60–100 °C threshold: BIOS Auto takes over only when CPU and GPU both reach it, then the selected manual state resumes after cooling
- Chinese and English UI
- Verified highest-privilege Windows logon task; startup goes directly to the tray
- Exact platform allowlist and `FEA3` `DIAG`/`DELL` signature validation
- Compiled command allowlist—there is no arbitrary SMM/register console
- The X button hides to the tray; the explicit Exit command restores both fans to Auto
- A companion watchdog retries Auto after an unexpected app exit
- One small x64 executable; no custom kernel driver and no TESTSIGNING mode

## Compatibility

Validated:

| Item | Value |
|---|---|
| Laptop | Dell G15 5515 |
| BIOS | 1.30.0 |
| OS | Windows 11 x64 |
| Fans | Two internal fans |
| Provider | One active `root\\dcim\\sysman\\diagnostics:LegacyDiags` instance |

The Dell WMI provider must already be present. It may be installed by Dell system software; if the namespace is missing, install or repair the current [Dell Command | Monitor](https://www.dell.com/support/kbdoc/en-us/000177080/dell-command-monitor) package from Dell. Dell does not officially list every consumer G-series configuration for Command Monitor, so provider availability must still be checked on each machine.

## Download and use

1. Download `DellG15FanControl.zip` from [Releases](https://github.com/cnlove7777777-art/fan/releases/latest).
2. Verify `SHA256SUMS.txt` if desired, extract the ZIP, then run `DellG15FanControl.exe` as administrator.
3. Windows may show a SmartScreen warning because the community build is not signed by a commercial code-signing certificate.
4. Choose 0 RPM, Dell Low, or Dell High and adjust the threshold slider (95 °C by default).
5. The X button hides the controller in the tray. Use **Exit and restore BIOS Auto** from the three-dot or tray menu to end it.

The three-dot menu can create a highest-privilege logon task named `DellG15LegacyFanControl`. The app immediately reads the task back and verifies its enabled state, EXE path, `--startup` argument, logon trigger, and privilege level. It does not enable test mode or install a driver.

## Temperature override

Dell Low and Dell High are firmware **states**, not arbitrary PWM or exact target-RPM values. Their observed RPM can differ between the two fans and with machine conditions; the displayed RPM is always read back from firmware.

The default threshold is 95 °C. BIOS Auto temporarily takes over only when both the CPU and NVIDIA GPU meet the threshold. Once that condition is no longer true, the last selected manual state is restored. If GPU telemetry is unavailable, automatic threshold switching is not performed and the UI shows `N/A`.

## Build from source

Windows includes the .NET Framework compiler used by this project:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The output is written to `dist\`. No NuGet package, WDK, EWDK, custom driver, BIOS image, or third-party binary is needed.

## How it works

```text
WinForms app
  -> Windows CIM/WMI (Dell LegacyDiags provider)
  -> Dell BIOS System Management Mode (SMM)
  -> Embedded Controller (EC)
  -> two internal fans
```

The command numbers and register layout are established Dell SMM conventions, independently documented by the upstream [Linux `dell-smm-hwmon` driver](https://github.com/torvalds/linux/blob/master/drivers/hwmon/dell-smm-hwmon.c). This Windows program does not run Linux code and contains no Linux kernel source; that implementation served as public protocol documentation. See [docs/PROTOCOL.md](docs/PROTOCOL.md).

## Safety and limitations

- `Off` can overheat a laptop if external cooling stops. Monitor temperatures.
- A Windows watchdog cannot recover during a complete OS/kernel hang or sudden power loss. A full power-off returns control to firmware.
- Dell firmware or another Dell utility may change the fan state. Avoid running AWCC/Thermal Control Center fan control at the same time.
- The first release uses a strict platform whitelist and is not a general Dell fan utility.
- The binary is reproducible from the included source but is currently unsigned.

Issues and verified compatibility reports are welcome. Include model, BIOS version, Windows version, whether `LegacyDiags` exists, and read-only status screenshots—never service tags or serial numbers.

## License

[MIT](LICENSE). Dell, Alienware and related marks belong to Dell Technologies. This is an independent community project and is not affiliated with Dell.

---

中文说明见 [README.zh-CN.md](README.zh-CN.md).
