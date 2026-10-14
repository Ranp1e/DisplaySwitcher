# DisplaySwitcher v2.1.0

Windows 桌面右键菜单「分辨率 / 刷新率」快捷切换工具。替代旧的 QRes.exe + BAT + VBS 方案：

- 不再有 BAT 黑框闪现（EXE 直接执行，执行完立即退出，不驻留后台）。
- 使用 `EnumDisplayDevices` + `ChangeDisplaySettingsEx` 原生 API，按设备名（`\\.\DISPLAY1` 等）精准定位显示器，多显示器（外接 + 笔记本）不会切错。
- **自定义分辨率输入**、**批量管理右键菜单**、**禁用/启用监视器**、**NVIDIA 缩放模式与数字振动调节**、**气泡 Toast 反馈**。

## 项目结构

```
DisplaySwitcher/
├── NativeMethods.cs    P/Invoke 定义：user32 显示 API + cfgmgr32/setupapi 设备管理 API
│                       + DisplayManager 封装（含 CDS_TEST 验证）
├── MonitorManager.cs   监视器 PNP 设备枚举（含已禁用）与禁用/启用（CM_*_DevNode）
├── NvApi.cs            NVAPI 动态加载（LoadLibrary nvapi64.dll + nvapi_QueryInterface），
│                       缩放模式（DISP_Get/SetDisplayConfig）与数字振动（DVC InfoEx）读写
├── Program.cs          入口、命令行解析、UAC 按需提权、CLI 各模式
├── DisplayConfig.cs    配置模型：DisplayProfile / ConfigRoot / NvidiaSettings（新）
│                       + LegacyDisplayConfig（旧格式兼容）
├── ConfigStore.cs      config.json 读写（JavaScriptSerializer），自动识别新旧格式
├── RegistryManager.cs  HKCU 桌面右键菜单注入/单项移除/批量移除
├── MainForm.cs         配置 GUI（纯代码布局，含 ToastForm 气泡）
├── app.manifest        asInvoker + dpiAware
├── build.bat           调用 .NET Framework 4.0 csc.exe 编译（C# 5 语法）
└── DisplaySwitcher.exe 编译产物（build.bat 生成）
```

## 构建

本机无需安装 dotnet SDK / MSBuild / Visual Studio，双击 `build.bat` 即可（调用 Windows 自带 csc.exe）。

## 界面说明（无参数双击打开）

- **显示器信息**：大字显示当前选中显示器的当前模式；【刷新检测】重新枚举全部状态。
- **自定义分辨率**：输入 宽 × 高 @ 刷新率（宽 640-7680 / 高 480-4320 / 24-500Hz），点【＋ 添加到列表】加入左侧可选列表（标注 `[自定义]`）。添加时用 `CDS_TEST` 验证；**驱动报告不支持时仍会添加**并气泡警告（自定义分辨率常依赖 GPU 缩放，`CDS_TEST` 可能误拒）。
- **监视器开关**：下拉列出系统所有监视器（含已禁用），开关即时经 UAC 提权执行禁用/启用；禁用前弹警告确认，且禁止禁用最后一台启用的显示器。
- **NVIDIA 显示增强**：缩放模式下拉（显示器缩放 / GPU缩放-保持纵横比 / GPU缩放-全屏 / 居中）+ 数字振动滑块（实时显示数值，点【应用 NVIDIA 设置】才写入，需 UAC 授权）。打开界面时实时读取当前值初始化；无 N 卡或驱动不支持时整卡灰置并注明原因。
- **桌面右键快捷方式（左右分栏）**：左侧「可选分辨率」按比例筛选（全部/16:9/16:10/4:3/21:9/5:4/其他）多选后【→ 添加选中项】即时写入 config.json + 注册表；右侧列出当前显示器已添加的项，可【← 移除选中项】或【全部移除】（需确认）。
- 所有操作反馈均为气泡 Toast；仅禁用监视器、全部移除等破坏性操作使用模态确认框。

## 命令行

```
DisplaySwitcher.exe                                       打开配置界面
DisplaySwitcher.exe --list                                列出显示器 + 监视器实例ID与启用状态
DisplaySwitcher.exe --switch "ID" [--elevated]            按 config.json 中的配置切换
DisplaySwitcher.exe --apply "\\.\DISPLAY1" 1920 1080 60 [--elevated]   直接按参数切换
DisplaySwitcher.exe --disable-monitor "实例ID" [--elevated]            禁用监视器（需管理员）
DisplaySwitcher.exe --enable-monitor "实例ID" [--elevated]             启用监视器（需管理员）
DisplaySwitcher.exe --nvidia-apply --display "\\.\DISPLAY1" --scaling 2 --vibrance 60   应用 NVIDIA 设置
DisplaySwitcher.exe --nvidia-probe                        NVAPI 只读诊断（DLL/函数/当前缩放/数字振动）
```

退出码：`0` 成功；`1` 参数错误；`2` 配置/设备不存在；`3` 显示切换失败；`4` 监视器/NVAPI 操作失败；`5` 安全拦截（禁用最后一台显示器）。

## config.json 格式与兼容

```json
{
  "profiles": [
    { "id": "xxxx", "deviceName": "\\\\.\\DISPLAY1", "friendlyName": "...", "width": 2088, "height": 1440, "refresh": 240 }
  ],
  "disabledMonitors": [],
  "nvidiaSettings": { "scalingMode": 2, "digitalVibrance": 50 }
}
```

**旧格式自动兼容**：v1/v2.0 的扁平数组格式（`[{"Id":"1b07911d",...}]`）读取时自动识别转换，`--switch "旧ID"` 继续可用，已有右键菜单项不受影响；写入时统一升级为新格式。`nvidiaSettings` 缺失时读默认值（scalingMode=0 未设置 / digitalVibrance=-1 未设置）。

## 关键实现说明

- **精准定位显示器**：`ChangeDisplaySettingsEx` 传入设备名，`dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY`；先 `CDS_FULLSCREEN | CDS_TEST` 验证再以 `CDS_UPDATEREGISTRY` 应用。
- **禁用/启用监视器**：`SetupDiGetClassDevs`（GUID_DEVCLASS_MONITOR）枚举含已禁用项；禁用用 `CM_Disable_DevNode` + `CM_DISABLE_PERSIST(0x8)` 跨重启保持（常量已对照官方 cfgmgr32.h 核对）。
- **NVAPI 动态加载**：`LoadLibrary("nvapi64.dll")`（32 位回退 `nvapi.dll`）+ `GetProcAddress("nvapi_QueryInterface")`；函数 ID 取自 NVIDIA 官方开源 `nvapi_interface.h`（DVC 两个 ID 不在开源表中，采用社区固定值）。不打包任何 DLL。
- **缩放模式**：`NvAPI_DISP_GetDisplayConfig` 读取全部 path → 仅修改目标 path 的 `details.scaling` 字段（`NV_DISPLAYCONFIG_PATH_ADVANCED_TARGET_INFO_V1` 偏移 8）→ `NvAPI_DISP_SetDisplayConfig`（`SAVE_TO_PERSISTENCE`）写回 → 重新读取验证。结构体按官方头文件 versioned struct 惯例手工封送。
- **数字振动**：`NvAPI_EnumNvidiaDisplayHandle` + `NvAPI_GetAssociatedNvidiaDisplayName` 按 `\\.\DISPLAYx` 匹配句柄 → `NvAPI_GetDVCInfoEx` 读 / `NvAPI_SetDVCLevelEx` 写（写前先读 min/max 做钳制）。
- **按需提权**：manifest 为 `asInvoker`；监视器开关与 NVIDIA 应用均以 runas 子进程执行；用户拒绝 UAC 时 NVIDIA 操作降级为进程内直接调用。
- **winexe 控制台输出**：CLI 模式通过 `AttachConsole(ATTACH_PARENT_PROCESS)` 附加到父控制台输出文本。
