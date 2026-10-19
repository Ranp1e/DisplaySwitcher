# DisplaySwitcher v2.6.0

Windows 桌面右键菜单「分辨率 / 刷新率」快捷切换工具。替代旧的 QRes.exe + BAT + VBS 方案：

- 不再有 BAT 黑框闪现（EXE 直接执行，执行完立即退出，不驻留后台）。
- 使用 `EnumDisplayDevices` + `ChangeDisplaySettingsEx` 原生 API，按设备名（`\\.\DISPLAY1` 等）精准定位显示器，多显示器（外接 + 笔记本）不会切错。
- **自定义分辨率输入**、**NVIDIA 驱动级创建/删除自定义分辨率**（替代 NVCP 手动流程）、**批量管理右键菜单**、**禁用/启用监视器**、**NVIDIA 缩放模式与数字振动调节**、**气泡 Toast 反馈**。

## 项目结构

```
DisplaySwitcher/
├── NativeMethods.cs    P/Invoke 定义：user32 显示 API + cfgmgr32/setupapi 设备管理 API
│                       + DisplayManager 封装（含 CDS_TEST 验证）
├── MonitorManager.cs   监视器 PNP 设备枚举（含已禁用）与禁用/启用（CM_*_DevNode）
├── NvApi.cs            NVAPI 动态加载（LoadLibrary nvapi64.dll + nvapi_QueryInterface），
│                       缩放模式（DISP_Get/SetDisplayConfig）与数字振动（DVC InfoEx）读写，
│                       驱动级自定义分辨率创建/删除（GetTiming/Try/Save/DeleteCustomDisplay）
├── AmdApi.cs           ADL 动态加载（LoadLibrary atiadlxx.dll，普通 C 导出），
│                       缩放模式（ImageExpansion + PreservedAspectRatio 组合）与饱和度（Color Get/Set）读写
├── Program.cs          入口、命令行解析、UAC 按需提权、CLI 各模式
├── DisplayConfig.cs    配置模型：DisplayProfile / ConfigRoot / NvidiaSettings（新）
│                       + LegacyDisplayConfig（旧格式兼容）
├── ConfigStore.cs      config.json 读写（JavaScriptSerializer），自动识别新旧格式
├── RegistryManager.cs  HKCU 桌面右键菜单注入/单项移除/批量移除（含旧命令去 --elevated 迁移）
├── ToastForm.cs        气泡 Toast 组件（GUI 与右键菜单命令行模式共用）
├── MainForm.cs         配置 GUI（纯代码布局）
├── app.manifest        asInvoker + dpiAware
├── build.bat           调用 .NET Framework 4.0 csc.exe 编译（C# 5 语法）
└── DisplaySwitcher.exe 编译产物（build.bat 生成）
```

## 构建

本机无需安装 dotnet SDK / MSBuild / Visual Studio，双击 `build.bat` 即可（调用 Windows 自带 csc.exe）。

## 界面说明（无参数双击打开）

- **显示器信息**：大字显示当前选中显示器的当前模式；【刷新检测】重新枚举全部状态。
- **自定义分辨率**：输入 宽 × 高 @ 刷新率（宽 640-7680 / 高 480-4320 / 24-500Hz），点【⚡ 创建到驱动】一步完成：按当前显示器的显卡自动路由——N 卡走 NVAPI（驱动自动算时序 → 硬件真实试验 → 保存 → 自动恢复桌面原模式），A 卡走 ADL `CustomizedMode_Validate/Add`（驱动自算时序）——成功后新模式立即出现在可选列表并**自动加入桌面右键菜单**。
- **【✕ 删除自定义（驱动）】**（N/A 卡通吃）：选中左侧任一模式后从驱动删除对应自定义分辨率（需确认），并连带移除引用它的右键菜单项与 config.json 配置；原生模式不在驱动自定义列表中，会明确拒绝删除。
- **监视器开关**：下拉只列当前实际插着的监视器（`DIGCF_PRESENT` 过滤历史幽灵设备，被禁用但仍连接的保留），开关即时经 UAC 提权执行禁用/启用；禁用前弹警告确认，且禁止禁用最后一台启用的显示器。
- **显示增强（NVIDIA / AMD 双面板）**：卡片左上角分段按钮切换厂商面板，外框尺寸不变。NVIDIA 面板：缩放模式（显示器缩放-纵横比/全屏）+ 数字振动滑块，作用于上方选中的显示器，直调失败才请求管理员授权。AMD 面板：缩放模式（GPU 缩放-纵横比/全屏）+ 饱和度滑块（ADL `Color Get/Set`），自动作用于 AMD 卡上第一个已连接显示器，无需管理员。默认厂商跟随当前显示器所属的卡；只有一家时另一家按钮灰掉；两家都没有时整卡灰置。打开界面时实时读取当前值，读取失败回退 config.json 中保存的值。
- **桌面右键快捷方式（左右分栏）**：左侧「可选分辨率」按比例筛选（全部/16:9/16:10/4:3/21:9/5:4/其他）多选后【→ 添加选中项】（或**双击直接添加**）即时写入 config.json + 注册表；右侧列出当前显示器已添加的项，可【← 移除选中项】（或**双击移除**）或【全部移除】（需确认）。【刷新检测】同时重读注册表右键菜单：自动修复失效路径、报告菜单项数，Win11 提示去「显示更多选项」查看。未选中项时相关按钮自动灰置；未保存配置时状态栏显示三步操作引导。
- **高 DPI / 可拉伸窗口**：iOS 风格卡片式布局（TableLayoutPanel/FlowLayoutPanel 容器，杜绝文字遮挡）；`AutoScaleMode.Dpi` 按系统缩放比例整体放大（4K 显示器正常显示），窗口可拖拽拉伸，列表区域随窗口变大，有最小尺寸保护。
- **【⚡ 测试切换】**：选中左侧任一模式后立即真实切换一次（非 CDS_TEST 空跑），成功/失败均弹气泡；用于在新机器（尤其是 A 卡）上快速验证切换链路。
- **右键菜单切换反馈**：`--switch`/`--apply` 执行后无论成败都弹气泡（失败带错误码与原因），并在 EXE 同目录追加写 `switch.log` 诊断日志；菜单命令不再带 `--elevated`（切换分辨率为当前用户设置，无需管理员，也不再弹 UAC）；启动界面时自动迁移旧菜单项，并把 command/Icon 中的 EXE 路径自愈为当前 EXE 路径（移动 EXE 后菜单不失效）。添加菜单项时逐条读回校验注册表（被安全软件拦截会明确报错并自动回滚）；气泡提示按系统区分（Win11 指引到右键桌面 →「显示更多选项」中查看）。
- 所有操作反馈均为气泡 Toast；仅禁用监视器、全部移除等破坏性操作使用模态确认框。

## 命令行

```
DisplaySwitcher.exe                                       打开配置界面
DisplaySwitcher.exe --list                                列出显示器 + 监视器实例ID与启用状态
DisplaySwitcher.exe --switch "ID" [--elevated]            按 config.json 中的配置切换（成功/失败均弹气泡并写 switch.log）
DisplaySwitcher.exe --apply "\\.\DISPLAY1" 1920 1080 60 [--elevated]   直接按参数切换
DisplaySwitcher.exe --disable-monitor "实例ID" [--elevated]            禁用监视器（需管理员）
DisplaySwitcher.exe --enable-monitor "实例ID" [--elevated]             启用监视器（需管理员）
DisplaySwitcher.exe --nvidia-apply --display "\\.\DISPLAY1" --scaling 2 --vibrance 60   应用 NVIDIA 设置
DisplaySwitcher.exe --nvidia-probe                        NVAPI 只读诊断（DLL/函数/当前缩放/数字振动）
DisplaySwitcher.exe --nvidia-enum-custom "\\.\DISPLAY1"   列出驱动中已保存的自定义分辨率及时序
DisplaySwitcher.exe --nvidia-create-custom "\\.\DISPLAY1" 1365 1440 240   创建自定义分辨率（驱动算时序+硬件试验+保存）
DisplaySwitcher.exe --nvidia-delete-custom "\\.\DISPLAY1" 1365 1440 240   删除驱动中的自定义分辨率
DisplaySwitcher.exe --amd-probe                           ADL 只读诊断（上下文/目标显示器/缩放/饱和度）
DisplaySwitcher.exe --amd-apply --scaling 6 --saturation 120   应用 AMD 设置（作用于 AMD 卡第一个已连接显示器）
```

退出码：`0` 成功；`1` 参数错误；`2` 配置/设备不存在；`3` 显示切换失败；`4` 监视器/NVAPI 操作失败；`5` 安全拦截（禁用最后一台显示器）。

## config.json 格式与兼容

```json
{
  "profiles": [
    { "id": "xxxx", "deviceName": "\\\\.\\DISPLAY1", "friendlyName": "...", "width": 2088, "height": 1440, "refresh": 240 }
  ],
  "disabledMonitors": [],
  "nvidiaSettings": { "scalingMode": 2, "digitalVibrance": 50 },
  "amdSettings": { "scalingMode": 6, "saturation": 100 }
}
```

**旧格式自动兼容**：v1/v2.0 的扁平数组格式（`[{"Id":"1b07911d",...}]`）读取时自动识别转换，`--switch "旧ID"` 继续可用，已有右键菜单项不受影响；写入时统一升级为新格式。`nvidiaSettings`/`amdSettings` 缺失时读默认值（scalingMode=0 未设置 / digitalVibrance=-1 未设置 / saturation=-1 未设置）。

## 关键实现说明

- **精准定位显示器**：`ChangeDisplaySettingsEx` 传入设备名；切换前先在驱动枚举的模式列表中智能匹配（宽高精确、刷新率 ±1 容差——驱动常把 60Hz 报成 59Hz），命中则**原样使用驱动给出的完整 DEVMODE**（含位深/显示标志），先 `CDS_TEST` 验证再以 `CDS_UPDATEREGISTRY` 应用；列表外的自定义分辨率才回退到手工构造的稀疏 DEVMODE。该策略兼容 NVIDIA/AMD 驱动（AMD 对稀疏 DEVMODE 与 `CDS_FULLSCREEN` 校验严格，旧实现会被拒绝）。
- **禁用/启用监视器**：`SetupDiGetClassDevs`（GUID_DEVCLASS_MONITOR）枚举含已禁用项；禁用用 `CM_Disable_DevNode` + `CM_DISABLE_PERSIST(0x8)` 跨重启保持（常量已对照官方 cfgmgr32.h 核对）。
- **NVAPI 动态加载**：`LoadLibrary("nvapi64.dll")`（32 位回退 `nvapi.dll`）+ `GetProcAddress("nvapi_QueryInterface")`；函数 ID 取自 NVIDIA 官方开源 `nvapi_interface.h`（DVC 两个 ID 不在开源表中，采用社区固定值）。不打包任何 DLL。
- **ADL 动态加载（AMD）**：`LoadLibrary("atiadlxx.dll")`（32 位回退 `atiadlxy.dll`，AMD 驱动自带），普通 C 导出按名解析；`ADL2_Main_Control_Create` 需传入内存分配回调（C# 委托常驻，驱动分配的 `ADLDisplayInfo` 缓冲用完 `FreeHGlobal`）。目标显示器 = 第一个 `DISPLAYCONNECTED` 的显示器。缩放 = `ImageExpansion`（GPU 缩放开关）+ `PreservedAspectRatio`（保持纵横比）组合；饱和度 = `ADL2_Display_Color_Get/Set`（`ADL_DISPLAY_COLOR_SATURATION`）；自定义分辨率 = `CustomizedMode_Validate → Add`（驱动自算时序），删除 = `CustomizedModeList_Get` 按宽高刷新率找索引后 `CustomizedMode_Delete`。结构体布局按 AMD 官方 ADL SDK 头文件手工封送。
- **驱动级自定义分辨率**：`NvAPI_DISP_GetTiming`（`NV_TIMING_OVERRIDE_AUTO`，与 NVCP「自动」同一公式，失败回退 CVT-RB）算时序 → `NvAPI_DISP_TryCustomDisplay` 硬件真实试验 → `NvAPI_DISP_SaveCustomDisplay` 保存 → `NvAPI_DISP_RevertCustomDisplayTrial` 恢复桌面原模式；删除用 `NvAPI_DISP_EnumCustomDisplay` 按宽高刷新率匹配取回原始时序后 `NvAPI_DISP_DeleteCustomDisplay`。`NV_CUSTOM_DISPLAY` 实测 144 字节、`NV_TIMING_INPUT` 实测 32 字节（均按官方头文件偏移手工封送）。
- **缩放模式**：`NvAPI_DISP_GetDisplayConfig` 读取全部 path → 仅修改目标 path 的 `details.scaling` 字段（`NV_DISPLAYCONFIG_PATH_ADVANCED_TARGET_INFO_V1` 偏移 8）→ `NvAPI_DISP_SetDisplayConfig`（`SAVE_TO_PERSISTENCE`）写回 → 重新读取验证。结构体按官方头文件 versioned struct 惯例手工封送。
- **数字振动**：`NvAPI_EnumNvidiaDisplayHandle` + `NvAPI_GetAssociatedNvidiaDisplayName` 按 `\\.\DISPLAYx` 匹配句柄 → `NvAPI_GetDVCInfoEx` 读 / `NvAPI_SetDVCLevelEx` 写（写前先读 min/max 做钳制）。
- **按需提权**：manifest 为 `asInvoker`；切换分辨率无需管理员（右键菜单命令不带 `--elevated`）；仅监视器开关与 NVIDIA 应用按需以 runas 子进程提权；用户拒绝 UAC 时 NVIDIA 操作降级为进程内直接调用。
- **winexe 控制台输出**：CLI 模式通过 `AttachConsole(ATTACH_PARENT_PROCESS)` 附加到父控制台输出文本。
