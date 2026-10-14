# DisplaySwitcher — 桌面右键菜单分辨率/刷新率切换工具

一个轻量化的 Windows 桌面工具，在桌面右键菜单一键切换指定显示器的分辨率与刷新率。专为需要在原生分辨率与自定义比例分辨率之间快速切换的 FPS 玩家设计。

> 本项目为旧 QRes + BAT + VBS 方案的完全重写（当前版本 v2.1.0），彻底放弃 QRes，改用 Windows 原生 API 实现。

## 特性

- **单一 EXE**：绿色便携，无需安装 .NET 运行时（Windows 10/11 原生支持）
- **多显示器精准定位**：通过 `EnumDisplayDevices` + `ChangeDisplaySettingsEx` 指定设备名（`\\.\DISPLAY1` 等），杜绝多屏切换错乱；设备名失效时按友好名兜底匹配
- **自定义分辨率输入**：直接输入任意 宽×高@刷新率（如 2088x1440@240），`CDS_TEST` 验证失败也允许添加（自定义分辨率常依赖 GPU 缩放）
- **左右分栏批量管理**：左侧按比例筛选可选模式（含自定义项），右侧管理当前显示器已注入的右键菜单项
- **禁用/启用监视器**：基于 cfgmgr32/setupapi 枚举与操作监视器 PNP 设备（含已禁用项），支持跨重启保持；带安全兜底——禁止禁用最后一台启用的显示器
- **NVIDIA 显示增强**：动态加载 nvapi64.dll（零打包依赖），支持缩放模式切换（显示器/GPU纵横比/GPU全屏/居中）与数字振动滑块调节，打开界面实时读取当前值
- **无后台驻留**：右键菜单项即调即走，执行完立即退出
- **无黑框闪现**：纯 WinForms 程序，无任何终端窗口
- **按需提权**：`asInvoker` 清单，仅切换/禁用/NVIDIA 应用动作触发 UAC；拒绝提权时自动降级
- **气泡反馈**：所有操作结果以圆角半透明 Toast 提示，自动淡出
- **配置兼容**：自动读取 v1/v2.0 旧格式 config.json，已有右键菜单项无缝可用

## 使用

1. 双击 `DisplaySwitcher.exe` 打开配置界面
2. 左侧「可选分辨率」按比例筛选并多选（或用「自定义分辨率」输入任意模式后【＋ 添加到列表】）
3. 点击【→ 添加选中项】即时写入右键菜单
4. 之后在桌面空白处右键即可一键切换；右侧列表可【← 移除选中项】/【全部移除】

## 命令行

```
DisplaySwitcher.exe                              # 打开配置 GUI
DisplaySwitcher.exe --list                       # 列出显示器及监视器实例ID/启用状态
DisplaySwitcher.exe --switch "配置ID" --elevated  # 切换（右键菜单实际调用方式）
DisplaySwitcher.exe --apply "\\.\DISPLAY1" 1920 1080 240
DisplaySwitcher.exe --disable-monitor "实例ID"     # 禁用监视器（需管理员，自动 UAC）
DisplaySwitcher.exe --enable-monitor "实例ID"      # 启用监视器
DisplaySwitcher.exe --nvidia-apply --display "\\.\DISPLAY1" --scaling 2 --vibrance 60
DisplaySwitcher.exe --nvidia-probe               # NVAPI 只读诊断
```

退出码：`0` 成功；`1` 参数错误；`2` 配置/设备不存在；`3` 切换失败；`4` 监视器/NVAPI 操作失败；`5` 安全拦截。

## 自行编译

无需 Visual Studio / dotnet SDK，使用 Windows 自带的 .NET Framework 编译器：

```bat
cd DisplaySwitcher
build.bat
```

## 项目结构

```
DisplaySwitcher/
├── DisplaySwitcher.exe   # 编译产物（单 EXE）
├── Program.cs            # 入口、命令行解析、UAC 按需提权
├── NativeMethods.cs      # P/Invoke：user32 显示 API + cfgmgr32/setupapi 设备 API
├── MonitorManager.cs     # 监视器枚举与禁用/启用
├── NvApi.cs              # NVAPI 动态加载：缩放模式 / 数字振动
├── MainForm.cs           # 配置 GUI（纯代码布局，含 Toast 气泡）
├── ConfigStore.cs        # config.json 读写（新旧格式兼容，EXE 同目录）
├── DisplayConfig.cs      # 配置模型（新格式 + 旧格式兼容模型 + NvidiaSettings）
├── RegistryManager.cs    # HKCU 桌面右键菜单注入/清理
├── app.manifest          # asInvoker + DPI 感知
└── build.bat             # csc.exe 一键编译脚本
```

## License

MIT
