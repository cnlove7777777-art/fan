# Security policy

## Supported platform

Only Dell G15 5515 with BIOS 1.30.0 is enabled in the current release. Unsupported platforms fail before a firmware call.

## Reporting

Please report a vulnerability privately through GitHub Security Advisories when possible. Do not attach service tags, serial numbers, memory dumps, private keys, or proprietary Dell binaries.

## Design boundaries

- The executable requires administrator rights because the Dell provider rejects ordinary users.
- Firmware calls are compiled into a seven-command allowlist.
- There is no arbitrary WMI register, physical-memory, EC, port-I/O, or SMM interface.
- Auto state 3 is attempted on normal exit and again by a separate watchdog after an unexpected process exit.
- The watchdog is best effort and cannot operate through an OS hang, firmware hang, sudden loss of power, or hardware failure.
