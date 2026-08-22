# Dell G15 5515 真风扇控制

这是一个小巧的 Windows 中英双语工具，为 **Dell G15 5515** 提供固件真实档位：**自动、停转（0 RPM）、低速、高速，以及软件温控曲线**。

它不是 AWCC/Thermal Control Center 那种“额外加速百分比”。本项目调用机器现有的 Dell `LegacyDiags` WMI/SMM 通道；在已经验证的 G15 5515 上，停转档会让两只内置风扇真实降到 0 RPM，正常关闭程序则会立刻恢复 BIOS 自动控制。

> 第一版只允许 **Dell G15 5515 + BIOS 1.30.0** 写入。没有完成同样的实机验证前，请不要删除机型和 BIOS 白名单。

## 功能

- 实时显示 CPU/GPU 温度、两只风扇档位和真实 RPM
- 自动 / 停转 / 低速 / 高速四种固件档位
- 基于四档的可调温度曲线
- 中文、English 运行时切换
- 可选开机自启动和启动后最小化
- 启动时核对准确机型、BIOS，以及 `FEA3` 的 `DIAG/DELL` 签名
- 程序里只有固定命令白名单，不开放任意 SMM/寄存器写入
- 正常退出时恢复两扇自动；程序意外结束时由独立看门狗再尝试恢复
- 单个约 28 KB 的 x64 EXE；不装自定义驱动，不需要 Windows 测试模式

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
4. 选择自动、停转、低速、高速，或者开启曲线。
5. 点窗口关闭会恢复 BIOS 自动；最小化会缩到托盘继续运行。

“开机自启动”会创建一个名为 `DellG15LegacyFanControl` 的最高权限登录任务。它不会打开 Windows 测试模式，也不会安装驱动。

## 默认曲线

- ≤ 50 °C：停转
- 51–65 °C：低速
- 66–80 °C：高速
- > 80 °C：自动，让 BIOS 接管

曲线控制的是固件四档，而不是任意 PWM 或精确 RPM。程序只在档位发生变化时写入，不会每秒反复调用；页面上的 RPM 始终来自固件回读。

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
