# Dell G15 5515 真风扇控制

这是一个紧凑的 Windows 中英双语工具，为 **Dell G15 5515** 提供三个固件真实手动档位：**停转（0 RPM）、Dell 低速、Dell 高速**，并带温度触发的 BIOS 自动接管。

它不是 AWCC/Thermal Control Center 那种“额外加速百分比”。本项目调用机器现有的 Dell `LegacyDiags` WMI/SMM 通道；在已经验证的 G15 5515 上，停转档会让两只内置风扇真实降到 0 RPM，正常关闭程序则会立刻恢复 BIOS 自动控制。

> 第一版只允许 **Dell G15 5515 + BIOS 1.30.0** 写入。没有完成同样的实机验证前，请不要删除机型和 BIOS 白名单。

## 功能

- 实时显示 CPU/GPU 温度、两只风扇档位和真实 RPM
- 真实 0 RPM / Dell 低速 / Dell 高速三个手动档
- 60–100°C 阈值：只有 CPU 和 GPU 同时达到时才临时切回 BIOS 自动，降温后恢复刚才选择的手动档
- 中文、English 运行时切换
- 可验证的最高权限开机任务，登录后直接常驻托盘
- 启动时核对准确机型、BIOS，以及 `FEA3` 的 `DIAG/DELL` 签名
- 程序里只有固定命令白名单，不开放任意 SMM/寄存器写入
- 点 X 只隐藏到托盘；明确选择“退出程序”时才恢复两扇 BIOS 自动
- 程序意外结束时由独立看门狗再尝试恢复
- 单个很小的 x64 EXE；不装自定义驱动，不需要 Windows 测试模式

## 已验证环境

| 项目 | 内容 |
|---|---|
| 电脑 | Dell G15 5515 |
| BIOS | 1.30.0 |
| 系统 | Windows 11 x64 |
| 风扇 | 两只内置风扇 |
| Dell 接口 | 一个活动的 `root\\dcim\\sysman\\diagnostics:LegacyDiags` 实例 |

电脑里必须已经有 Dell 的 WMI provider。如果提示找不到 `LegacyDiags`，请从 Dell 官网安装或修复最新版 [Dell Command | Monitor](https://www.dell.com/support/kbdoc/en-us/000177080/dell-command-monitor)。Dell 没有为每一种消费级 G 系列配置都作出官方支持承诺，因此仍需逐台检查 provider 是否存在。

## 下载和使用

1. 从 [Releases](https://github.com/cnlove7777777-art/fan/releases/latest) 下载 `DellG15FanControl.zip`。
2. 解压后以管理员身份运行 `DellG15FanControl.exe`。
3. 社区 EXE 暂时没有商业代码签名证书，Windows 可能弹出 SmartScreen 提醒。
4. 选择 0 RPM、Dell 低速或 Dell 高速，并调整阈值滑条（默认 95°C）。
5. 点窗口 X 会缩到托盘继续控制。需要真正结束时，从三点菜单或托盘菜单选择“退出程序并恢复 BIOS 自动”。

三点菜单里的“开机自启动”会创建名为 `DellG15LegacyFanControl` 的最高权限登录任务。创建后程序会立即反查并核对：是否启用、EXE 路径、`--startup` 参数、登录触发器和权限级别；失败时会明确提醒检查管理员权限。它不会打开 Windows 测试模式，也不会安装驱动。

## 温度接管规则

Dell 低速和 Dell 高速是固件离散档位，不是任意 PWM，也不是精确固定 RPM。两只风扇的实际转速会有差异，也可能随机器状态变化；页面上的 RPM 始终来自固件回读。

默认阈值是 95°C。只有 CPU 和 NVIDIA GPU 同时达到阈值时，程序才临时切换为 BIOS 自动；这个“双高温”条件不再成立后，恢复最后选择的手动档。如果 GPU 温度无法读取，页面显示 `N/A`，不会执行自动阈值切换。

## 源码构建

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

输出在 `dist\`。构建只使用 Windows 自带的 .NET Framework C# 编译器，不下载 NuGet、WDK/EWDK、驱动、BIOS 或第三方二进制文件。

## 原理

```text
WinForms 程序
  -> Windows CIM/WMI（Dell LegacyDiags provider）
  -> Dell BIOS 的 System Management Mode（SMM）
  -> Embedded Controller（EC）
  -> 两只内置风扇
```

Linux 内核上游的 [`dell-smm-hwmon` 源码](https://github.com/torvalds/linux/blob/master/drivers/hwmon/dell-smm-hwmon.c)公开记录了同一组 Dell SMM 命令和寄存器约定。本程序运行的是 Windows C#，不执行也不复制 Linux 内核代码；Linux 源码在这里是公开协议资料。更完整的说明见 [docs/PROTOCOL.md](docs/PROTOCOL.md)。

## 边界

- 外置风扇失效时，0 RPM 可能让笔记本迅速升温，请看着温度。
- Windows 看门狗无法跨越整机死机或内核挂死；完全关机/断电会让固件重新接管。
- AWCC、Thermal Control Center 或其他 Dell 工具可能改写档位，使用本程序时不要同时让它们控制风扇。
- 第一版不是通用 Dell 工具，只支持已实测组合。
- 当前发布 EXE 未使用商业证书签名，但源码和构建脚本完整公开。

欢迎提交实机兼容报告：请给出机型、BIOS、Windows 版本、是否存在 `LegacyDiags`，以及只读状态截图；请勿上传服务编号或序列号。

## 许可

[MIT](LICENSE)。Dell、Alienware 等商标归 Dell Technologies 所有。本项目为独立社区项目，与 Dell 无隶属或官方合作关系。
