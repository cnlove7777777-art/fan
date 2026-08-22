# Dell G15 5515 True Fan Control

A small bilingual Windows utility that gives the **Dell G15 5515** real firmware fan modes: **Auto, Off (0 RPM), Low, High, and a software curve**.

Unlike AWCC-style “fan boost” sliders, this project calls Dell's existing `LegacyDiags` WMI/SMM interface. On the validated machine, `Off` genuinely stops both internal fans and closing the program immediately restores BIOS automatic control.

> First public release is intentionally restricted to **Dell G15 5515 + BIOS 1.30.0**. Do not remove the model/BIOS allowlist unless you have independently validated the same commands and recovery behavior.

## Features

- Real-time CPU temperature, NVIDIA GPU temperature, fan state and RPM
- Firmware states: Auto / Off / Low / High
- Configurable temperature curve using those four discrete states
- Chinese and English UI
- Optional elevated Windows logon task and start-minimized mode
- Exact platform allowlist and `FEA3` `DIAG`/`DELL` signature validation
- Compiled command allowlist—there is no arbitrary SMM/register console
- Restores both fans to Auto on normal exit; a companion watchdog retries after an unexpected app exit
- One 28 KB x64 executable; no custom kernel driver and no TESTSIGNING mode

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
4. Choose Auto, Off, Low, High, or enable Curve.
5. Closing the window restores BIOS Auto. Minimizing sends the app to the tray.

The startup checkbox creates a highest-privilege logon task named `DellG15LegacyFanControl`. It does not enable test mode and does not install a driver.

## Curve behavior

The curve controls firmware **states**, not arbitrary PWM/RPM values. Defaults:

- up to 50 °C: Off
- 51–65 °C: Low
- 66–80 °C: High
- above 80 °C: Auto (BIOS takes over)

Writes occur only when the selected state changes. The displayed RPM is always read back from firmware.

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
