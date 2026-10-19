# DisplaySwitcher 项目交接文档（会话转移用）

> 导出时间：2026-10-14。新窗口接手时把本文档发给 AI 即可恢复全部上下文。
>
> **2026-10-16 更新**：后续迭代已转移到工作区 `E:\desktop\DisplaySwitcher 工作区`。
> A 卡兼容版 v2.2.0（免 UAC 菜单命令、A 卡 DEVMODE 修复、切换气泡+switch.log）见
> `此文件夹中是在A卡台式机中聊天记录与迭代记录/`；**v2.3.0** 在 `DisplaySwitcher-v2.3.0/`（完整源码+已编译 EXE），新增：
> ① 注册表写入读回校验 + Win11「显示更多选项」指引气泡；② NVIDIA 驱动级创建/删除自定义分辨率
> （`NvAPI_DISP_GetTiming` AUTO 算时序 → Try → Save → Revert；`NV_CUSTOM_DISPLAY`=144 字节、
> `NV_TIMING_INPUT`=32 字节，均已真机验证）；③ GUI 新增【⚡ 创建到驱动】【✕ 删除自定义（驱动）】（连带清理右键菜单项+config），A 卡自动灰置。
> **2026-10-17 更新**：**v2.4.0** 在 `DisplaySwitcher-v2.4.0/`（含 config.json，避免菜单迁移后找不到配置）。
> 新增 AMD 显示增强：新增 `AmdApi.cs`（ADL 动态加载 atiadlxx.dll：ImageExpansion+PreservedAspectRatio
> 组合缩放、Color Get/Set 饱和度），「显示增强」卡片改为左上角分段按钮切换 NVIDIA/AMD 双面板（外框尺寸不变），
> AMD 面板自动作用于 AMD 卡第一个已连接显示器；config.json 加 `amdSettings`；CLI 加 `--amd-probe`/`--amd-apply`。
> **2026-10-18 更新**：**v2.5.0** 在 `DisplaySwitcher-v2.5.0/`（当前最新，右键菜单已指向它）。按用户 6 点反馈：
> ① 刷新检测重读注册表右键菜单（自愈失效路径+项数+Win11 提示）；② 自定义分辨率一键创建到驱动，
> **A 卡用 ADL CustomizedMode_Validate/Add（驱动自算时序）**，N 卡走 NVAPI，成功后自动入右键菜单，
> 删除自定义双厂商支持；删掉了【＋ 添加到列表】和会话内 customEntries 机制；③ 显示增强双面板用户已确认 OK；
> ④ 监视器开关用 DIGCF_PRESENT 过滤幽灵设备（修了别的电脑列出一排重复监视器的 bug），状态读不出标"未知"；
> ⑤ AutoScaleMode.Font + 窗口可拉伸（Sizable + 全控件 Anchor + MinimumSize），修 4K 高 DPI 布局；
> ⑥ 双击左侧=添加、双击右侧=移除、按钮随选择灰置、状态栏三步引导。
> **2026-10-19 更新**：**v2.6.0** 在 `DisplaySwitcher-v2.6.0/`（当前最新，右键菜单已指向它，config 已与注册表对齐）。
> A 卡机实测 v2.5.0：显示增强/监视器开关/创建到驱动提示不支持（A 卡 4K 屏，放弃）均符合预期；UI 文字遮挡。
> v2.6.0 = 纯美化重构：iOS 风格卡片（灰底白卡+系统蓝/绿），布局全部改为布局容器
> （卡片 Dock=Top 堆叠 + TableLayout/FlowLayout 内部排布），功能逻辑零改动；修了 TableLayoutPanel
> Percent 行 + AutoScroll 压扁、FlowLayoutPanel 换行测量虚报两个布局坑；截图验证通过。
> 发布包：`dist/DisplaySwitcher-v2.6.0.zip`（v2.5.0.zip 留作回退）。
> 主仓库 `DisplaySwitcher/` 仍为 v2.1.0 未动，等用户确认后再合。
> **教训：任何真实改变显示模式的验证（切换/TryCustomDisplay）执行前必须先征得用户同意。**


## 一、项目概况

- **项目**：DisplaySwitcher — Windows 桌面右键菜单分辨率/刷新率快捷切换工具（FPS 玩家向）
- **目录**：`E:\desktop\TooLs\QRes\`（git 仓库根），代码在 `DisplaySwitcher/` 子目录
- **GitHub**：`github.com/Ranp1e/DisplaySwitcher`（原名 Context-Menu-Resolution-Switcher，已改名，本地 remote 已更新）
- **当前版本**：v2.1.0，commit `1293ec2` 已推送 main 分支
- **用户身份**：GitHub 用户 Ranp1e，git 配置 user.name=Ranp1e / user.email=Ranp1e@users.noreply.github.com（仅本仓库）
- **机器显示器**：`\\.\DISPLAY1`，NVIDIA RTX 4060 Laptop GPU，原生 1920x1080@240Hz

## 二、技术栈硬约束（用户严格要求）

- C# WinForms，**.NET Framework 4.x，C# 5 语法**（无插值字符串、无 `?.`、无表达式体成员等）
- 编译器：Windows 自带 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，无 dotnet SDK/MSBuild/VS
- 编译命令在 `DisplaySwitcher/build.bat`；**Git Bash 里手动调 csc 必须加 `MSYS2_ARG_CONV_EXCL='*'` 且参数用 `-` 前缀**（防路径转换）
- 零外部依赖（无 NuGet/第三方库/外部工具）；NVAPI 只能动态 `LoadLibrary("nvapi64.dll")`
- 纯代码布局 GUI，禁用设计器；反馈用自绘 Toast 气泡，不用 MessageBox（确认框除外）
- asInvoker manifest；仅切换/禁用监视器时 runas 提权（实测 NVAPI 操作不需要管理员）
- 无后台驻留：右键菜单调带参 EXE，执行完即退出

## 三、功能现状（全部已实现并验证）

1. **分辨率切换**：EnumDisplayDevices 枚举设备名+友好名，ChangeDisplaySettingsEx 指定设备名，CDS_TEST→CDS_UPDATEREFISTRY；设备名失效按友好名兜底，失败退出码 2
2. **UI**：显示器信息卡片（大字当前模式+刷新检测+显示器下拉）、自定义分辨率输入行（宽640-7680/高480-4320/频率24-500，CDS_TEST 失败仍可强制添加）、监视器开关卡片（下拉+Toggle）、NVIDIA 显示增强卡片、左右分栏（左侧按刷新率筛选+多选添加 / 右侧已添加项+移除）
3. **禁用/启用监视器**：cfgmgr32 `CM_Disable_DevNode`/`CM_Enable_DevNode`（CM_DISABLE_UI_NOT_OK|CM_DISABLE_PERSIST=0x4|0x8），SetupDi 枚举含禁用设备；警告框+UAC 提权；禁止禁用最后一台启用显示器（退出码 5）
4. **NVIDIA 显示增强**：缩放模式仅两项「纵横比(NV_SCALING=6)/全屏(=1)」固定显示器缩放；数字振动 0-100 滑块；点【应用】即时直调生效，失败才降级提权；结果真实反馈气泡；成功才写 config.json
5. **右键菜单**：`HKCU\Software\Classes\DesktopBackground\Shell\DisplaySwitcher.<Id>`，MUIVerb=`切换到 2560x1440 @240Hz`，command=`"exe" --switch "Id" --elevated`；按 宽x高x刷新率 去重；右侧列表数据源=注册表∪config 并集
6. **CLI**：`--list` / `--switch "ID"` / `--apply` / `--disable-monitor` / `--enable-monitor` / `--nvidia-apply --display X --scaling N --vibrance NN` / `--nvidia-probe`（只读诊断）/ `--cleanup-registry`（去重清理）
7. **config.json**：新格式 `{profiles:[{id,deviceName,friendlyName,width,height,refresh}], disabledMonitors:[], nvidiaSettings:{scalingMode,digitalVibrance}}`；旧扁平数组格式自动迁移兼容，ID 不变

## 四、关键坑与结论（重要！）

- **SetDisplayConfig 崩溃根因**：`sourceModeInfo` 传 NULL 会让驱动持久化路径（flags=2 SAVE_TO_PERSISTENCE）解引用空指针 → AccessViolationException 静默崩死提权子进程。修复=为每条 path 分配 32 字节 SourceModeInfo 缓冲区。flags=0+NULL 不崩但静默不生效。参考：DisplayMagician 项目 NVIDIALibrary.cs
- **NVAPI 函数 ID** 来自 github.com/NVIDIA/nvapi 官方开源 nvapi_interface.h；DVC 两个 ID（GetDVCInfoEx=0x0E45002D, SetDVCLevelEx=0x4A82C2B1）不在开源表，用社区固定值，已实测可用
- **结构体尺寸已核对**：PATH_INFO_V2 x64=48/x86=28，TARGET_INFO_V2=24/12，ADVANCED_TARGET_INFO_V1=128（scaling 在偏移 8）
- **Bash 传设备名**：`\\.\DISPLAY1` 经 JSON+bash 双重转义会被吃掉反斜杠，正确写法是单引号 `'\\\\.\\DISPLAY1'`（JSON 层）
- **原生分辨率下改缩放模式屏幕无可见变化是正常的**，要切非原生分辨率（如 1568x1080）才能看出拉伸差异
- **数字振动/缩放实测不需要管理员**
- 崩溃的提权子进程会变僵尸挂住 EXE 文件锁，需 taskkill 后才能重新编译

## 五、发布渠道状态

- **GitHub Release v2.0.0** 已发（旧版）；**v2.1.0 代码已推送，Release 由用户手动发布**（zip 在 `E:\desktop\TooLs\QRes\dist\DisplaySwitcher-v2.1.0.zip`，33KB）
- **蓝奏云**：用户打算面向抖音观众分发（已注册与否未知）。要点：下载者免登录不限速；域名常被墙可换 lanzoui/lanzoux 等字母变体；抖音会吞外链（用空格断开/个人简介/口令）；建议文件名中文化
- gh CLI 未安装，Release 只能用户手动网页发（或装 gh/提供 PAT 后可自动化）

## 六、遗留/注意

- GUI 布局修复后用户已实机确认「好用」
- `config.json`（用户个人配置）仍在仓库中跟踪，曾建议移除用户未表态
- `disabledMonitors` 字段仅保留无消费逻辑
- 僵尸进程残留属系统回收范畴，无害
