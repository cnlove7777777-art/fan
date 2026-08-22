# Protocol notes

This project deliberately exposes only seven allowlisted Dell SMM calls through the Windows `LegacyDiags.Execute` WMI method.

| EAX | Meaning | EBX input |
|---:|---|---|
| `FEA3` | revision/signature | `0`, with `ECX=0x20` |
| `00A3` | fan state | fan index |
| `01A3` | set fan state | fan index OR state shifted left 8 |
| `02A3` | current fan RPM | fan index |
| `04A3` | nominal RPM | fan index OR state shifted left 8 |
| `10A3` | temperature | sensor index |
| `11A3` | sensor type | sensor index |

The revision probe must return EAX `0x44494147` (`DIAG`) and EDX `0x44454C4C` (`DELL`). Registers are passed as four-byte little-endian arrays with a length field of four.

Validated fan states on Dell G15 5515 / BIOS 1.30.0:

| State | Result |
|---:|---|
| 0 | Off / physical 0 RPM |
| 1 | Low |
| 2 | High |
| 3 | BIOS automatic control |

These are discrete firmware states; they are not arbitrary target RPM values.

Primary public reference: the upstream Linux [`dell-smm-hwmon.c`](https://github.com/torvalds/linux/blob/master/drivers/hwmon/dell-smm-hwmon.c), including `I8K_SMM_SET_FAN`, `I8K_SMM_GET_FAN`, `I8K_SMM_GET_SPEED`, `I8K_SMM_GET_NOM_SPEED`, temperature calls and Dell signature calls. The Linux driver is GPL-2.0-or-later; this repository contains an independent C# implementation based on the protocol facts and does not copy kernel source.

Windows transport on the validated machine:

```text
namespace: root\dcim\sysman\diagnostics
class:     LegacyDiags
method:    Execute
```

No Dell executable or DLL is redistributed.
