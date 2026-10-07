# DisplaySwitcher

Windows 桌面右键菜单「分辨率 / 刷新率」快捷切换工具。替代旧的 QRes.exe + BAT + VBS 方案：

- 不再有 BAT 黑框闪现（EXE 直接执行，执行完立即退出，不驻留后台）。
- 使用 `EnumDisplayDevices` + `ChangeDisplaySettingsEx` 原生 API，按设备名（`\\.\DISPLAY1` 等）精准定位显示器，多显示器（外接 + 笔记本）不会切错。

## 项目结构

```
DisplaySwitcher/
├── NativeMethods.cs    P/Invoke 定义（DISPLAY_DEVICE / DEVMODE / EnumDisplayDevices /
│                       EnumDisplaySettings / ChangeDisplaySettingsEx）+ DisplayManager 封装
├── Program.cs          入口、命令行解析、UAC 按需提权、--list / --switch / --apply
├── DisplayConfig.cs    配置模型（Id / DeviceName / FriendlyName / Width / Height / Frequency / MenuName）
├── ConfigStore.cs      config.json 读写（JavaScriptSerializer），文件固定在 EXE 同目录
├── RegistryManager.cs  HKCU 桌面右键菜单注入与移除
├── MainForm.cs         极简配置 GUI（纯代码布局）
├── app.manifest        asInvoker + dpiAware
├── build.bat           调用 .NET Framework 4.0 csc.exe 编译（C# 5 语法）
└── DisplaySwitcher.exe 编译产物（build.bat 生成）
```

## 构建

本机无需安装 dotnet SDK / MSBuild / Visual Studio，直接用系统自带的 csc.exe：

```
双击 build.bat
```

等价命令：

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /platform:anycpu /utf8output /codepage:65001 /win32manifest:app.manifest /out:DisplaySwitcher.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll *.cs
```

## 使用

### 1. 配置（GUI）

无参数双击 `DisplaySwitcher.exe` 打开配置界面：

1. 选择显示器（格式 `\\.\DISPLAY1 (友好名)`）。
2. 选择分辨率、刷新率（刷新率随所选分辨率联动），菜单名称会自动填充建议名，可修改。
3. 【测试切换】：先记录当前模式 → 以 runas 启动自身 `--apply` 切换 → 弹出 15 秒倒计时对话框，超时或点「立即还原」自动恢复原模式。
4. 【确认并保存到右键菜单】：写入 `config.json` + 注册表右键菜单项。
5. 【从右键菜单移除】：删除本软件创建的所有注册表项，可选同时清空 `config.json`。

保存后在**桌面空白处右键**即可看到菜单项，点击即切换。

### 2. 命令行

```
DisplaySwitcher.exe                                   打开配置界面
DisplaySwitcher.exe --list                            列出所有显示器（诊断用）
DisplaySwitcher.exe --switch "配置ID" [--elevated]     按 config.json 中的配置切换
DisplaySwitcher.exe --apply "\\.\DISPLAY1" 1920 1080 60 [--elevated]   直接按参数切换
```

退出码：`0` 成功；`1` 参数错误；`2` 配置或显示器不存在；`3` 切换失败。

## 关键实现说明

- **精准定位显示器**：`ChangeDisplaySettingsEx` 传入设备名（如 `\\.\DISPLAY1`），而非全局的 `ChangeDisplaySettings`；DEVMODE 的 `dmFields` 包含 `DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY`。切换前先用 `CDS_FULLSCREEN | CDS_TEST` 测试模式合法性，再以 `CDS_UPDATEREGISTRY` 应用。
- **按需提权**：manifest 为 `asInvoker`；命令行带 `--elevated` 且当前非管理员时，用 `Process.Start`（`Verb="runas"`）重启自身并去掉 `--elevated`，随后立即退出。用户拒绝 UAC 时降级为直接切换（`ChangeDisplaySettingsEx` 写 HKCU 通常无需管理员）。
- **注册表注入**：`HKCU\Software\Classes\DesktopBackground\Shell\DisplaySwitcher.<Id>`，写入 `MUIVerb`（菜单名）、`Icon`（EXE 路径）、子键 `command` 默认值 `"exe路径" --switch "Id" --elevated`。移除时遍历 Shell 下所有 `DisplaySwitcher.` 前缀子键删除。全程仅 HKCU，无需管理员。
- **DeviceName 失效兜底**：`--switch` 时若 `\\.\DISPLAYx` 已不存在，按保存的显示器友好名（`FriendlyName`）重新匹配设备名，仍失败则退出码 2。
- **winexe 控制台输出**：`--list` 等模式通过 `AttachConsole(ATTACH_PARENT_PROCESS)` 附加到父 cmd 窗口输出文本。
