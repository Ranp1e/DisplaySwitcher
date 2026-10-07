# DisplaySwitcher — 桌面右键菜单分辨率/刷新率切换工具

一个轻量化的 Windows 桌面工具，在桌面右键菜单一键切换指定显示器的分辨率与刷新率。专为需要在原生分辨率与自定义比例分辨率之间快速切换的 FPS 玩家设计。

> 本项目为旧 QRes + BAT + VBS 方案的完全重写（v2.0.0），彻底放弃 QRes，改用 Windows 原生 API 实现。

## 特性

- **单一 EXE，仅 ~25 KB**：绿色便携，无需安装 .NET 运行时（Windows 10/11 原生支持）
- **多显示器精准定位**：通过 `EnumDisplayDevices` + `ChangeDisplaySettingsEx` 指定设备名（`\\.\DISPLAY1` 等），杜绝多屏切换错乱；设备名失效时按友好名兜底匹配
- **无后台驻留**：右键菜单项即调即走，执行完立即退出
- **无黑框闪现**：纯 WinForms 程序，无任何终端窗口
- **按需提权**：`asInvoker` 清单，仅切换动作触发 UAC；拒绝提权时自动降级直切
- **切换安全**：先 `CDS_TEST` 验证模式再应用；测试切换带 15 秒倒计时自动还原

## 下载

前往 [Releases](../../releases) 页面下载 `DisplaySwitcher-v2.0.0.zip`，解压即用。

## 使用

1. 双击 `DisplaySwitcher.exe` 打开配置界面
2. 选择显示器、分辨率、刷新率，自定义菜单显示名称
3. 点击【测试切换】验证效果（15 秒内可自动还原）
4. 点击【确认并保存到右键菜单】
5. 之后在桌面空白处右键即可一键切换

清理：配置界面点击【从右键菜单移除】可删除所有已注入的菜单项。

## 命令行

```
DisplaySwitcher.exe                              # 打开配置 GUI
DisplaySwitcher.exe --list                       # 列出所有显示器及当前模式
DisplaySwitcher.exe --switch "配置ID" --elevated  # 切换（右键菜单实际调用方式）
DisplaySwitcher.exe --apply "\\.\DISPLAY1" 1920 1080 240
```

## 自行编译

无需 Visual Studio / dotnet SDK，使用 Windows 自带的 .NET Framework 编译器：

```bat
cd DisplaySwitcher
build.bat
```

## 项目结构

```
DisplaySwitcher/
├── DisplaySwitcher.exe   # 编译产物（单 EXE，~25 KB）
├── Program.cs            # 入口、命令行解析、UAC 按需提权
├── NativeMethods.cs      # P/Invoke：EnumDisplayDevices / ChangeDisplaySettingsEx / DEVMODE
├── MainForm.cs           # 极简配置 GUI
├── ConfigStore.cs        # config.json 读写（EXE 同目录）
├── DisplayConfig.cs      # 配置模型
├── RegistryManager.cs    # HKCU 桌面右键菜单注入/清理
├── app.manifest          # asInvoker + DPI 感知
└── build.bat             # csc.exe 一键编译脚本
```

## License

MIT
