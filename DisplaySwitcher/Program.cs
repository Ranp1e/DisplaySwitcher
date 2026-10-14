using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

namespace DisplaySwitcher
{
    internal static class Program
    {
        // 退出码：0 成功；1 参数错误；2 配置/设备不存在；3 显示切换失败；4 监视器操作失败；5 安全拦截
        private static bool consoleAttached;

        [STAThread]
        private static int Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }

            // winexe 默认没有控制台，附加到父控制台以便输出
            AttachParentConsole();
            try
            {
                // 按需提权：带 --elevated 且当前不是管理员时，用 runas 重启自身（去掉 --elevated）
                if (HasFlag(args, "--elevated") && !IsAdministrator())
                {
                    try
                    {
                        RestartElevated(RemoveFlag(args, "--elevated"));
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        // 用户拒绝 UAC 时降级：仍直接尝试执行
                        Out("UAC 提权被取消或失败，改为直接尝试执行：" + ex.Message);
                    }
                }

                string cmd = args[0];
                if (cmd == "--list") return DoList();
                if (cmd == "--switch")
                {
                    if (args.Length < 2) { Out("用法: DisplaySwitcher.exe --switch \"配置ID\""); return 1; }
                    return DoSwitch(args[1]);
                }
                if (cmd == "--apply")
                {
                    if (args.Length < 5) { Out("用法: DisplaySwitcher.exe --apply \"设备名\" 宽 高 刷新率"); return 1; }
                    return DoApply(args[1], args[2], args[3], args[4]);
                }
                if (cmd == "--disable-monitor")
                {
                    if (args.Length < 2) { Out("用法: DisplaySwitcher.exe --disable-monitor \"监视器实例ID\""); return 1; }
                    return DoMonitor(args[1], true);
                }
                if (cmd == "--enable-monitor")
                {
                    if (args.Length < 2) { Out("用法: DisplaySwitcher.exe --enable-monitor \"监视器实例ID\""); return 1; }
                    return DoMonitor(args[1], false);
                }
                if (cmd == "--nvidia-apply")
                {
                    string display = null;
                    int scaling = -1, vibrance = -1, tmp;
                    for (int i = 1; i + 1 < args.Length; i++)
                    {
                        if (args[i] == "--display") { display = args[i + 1]; i++; }
                        else if (args[i] == "--scaling" && int.TryParse(args[i + 1], out tmp)) { scaling = tmp; i++; }
                        else if (args[i] == "--vibrance" && int.TryParse(args[i + 1], out tmp)) { vibrance = tmp; i++; }
                    }
                    return DoNvidiaApply(display, scaling, vibrance);
                }
                if (cmd == "--nvidia-probe") return DoNvidiaProbe();
                if (cmd == "--cleanup-registry") return DoCleanupRegistry();

                Out("未知参数。用法：");
                Out("  DisplaySwitcher.exe                                       打开配置界面");
                Out("  DisplaySwitcher.exe --list                                列出显示器与监视器设备");
                Out("  DisplaySwitcher.exe --switch \"ID\" [--elevated]            按配置切换");
                Out("  DisplaySwitcher.exe --apply \"\\\\.\\DISPLAYx\" 宽 高 刷新率 [--elevated]");
                Out("  DisplaySwitcher.exe --disable-monitor \"实例ID\" [--elevated]");
                Out("  DisplaySwitcher.exe --enable-monitor \"实例ID\" [--elevated]");
                Out("  DisplaySwitcher.exe --nvidia-apply --display \"\\\\.\\DISPLAYx\" --scaling N --vibrance NN");
                Out("  DisplaySwitcher.exe --nvidia-probe                          NVAPI 只读诊断");
                Out("  DisplaySwitcher.exe --cleanup-registry                      右键菜单去重清理");
                return 1;
            }
            finally
            {
                FinishConsole();
            }
        }

        // ---------- 命令行模式 ----------

        private static int DoList()
        {
            List<DisplayInfo> list = DisplayManager.GetDisplays();
            if (list.Count == 0)
            {
                Out("未发现已连接到桌面的显示器。");
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    DisplayInfo d = list[i];
                    string friendly = string.IsNullOrEmpty(d.MonitorName) ? d.AdapterString : d.MonitorName;
                    Out(d.DeviceName + "  " + friendly + "  当前: "
                        + d.CurrentWidth + "x" + d.CurrentHeight + " @" + d.CurrentFrequency + "Hz");
                    if (!string.IsNullOrEmpty(d.MonitorInstanceId))
                        Out("    监视器实例ID: " + d.MonitorInstanceId);
                }
            }

            List<MonitorDevice> monitors = MonitorManager.GetMonitors();
            Out("");
            Out("监视器设备（含已禁用）：");
            if (monitors.Count == 0)
            {
                Out("  （未枚举到监视器设备）");
            }
            else
            {
                for (int i = 0; i < monitors.Count; i++)
                {
                    MonitorDevice m = monitors[i];
                    Out("  " + m.InstanceId + "  " + m.FriendlyName + (m.IsEnabled ? "  [已启用]" : "  [已禁用]"));
                }
            }
            return 0;
        }

        private static int DoSwitch(string id)
        {
            DisplayProfile cfg;
            try
            {
                cfg = ConfigStore.FindById(id);
            }
            catch (Exception ex)
            {
                Out("读取 config.json 失败：" + ex.Message);
                return 2;
            }
            if (cfg == null)
            {
                Out("config.json 中找不到配置 ID：" + id);
                return 2;
            }

            string device = cfg.deviceName;
            if (!DisplayManager.DeviceExists(device))
            {
                string alt = DisplayManager.FindDeviceByFriendlyName(cfg.friendlyName);
                if (alt == null)
                {
                    Out("显示器 " + device + " 当前不存在，且无法按友好名匹配。");
                    return 2;
                }
                Out("设备名 " + device + " 已失效，按友好名匹配到 " + alt);
                device = alt;
            }

            int r = DisplayManager.SetMode(device, cfg.width, cfg.height, cfg.refresh);
            if (r == NativeMethods.DISP_CHANGE_SUCCESSFUL)
            {
                Out("切换成功：" + device + " -> " + cfg.width + "x" + cfg.height + " @" + cfg.refresh + "Hz");
                return 0;
            }
            if (r == NativeMethods.DISP_CHANGE_BADMODE)
                Out("切换失败：该分辨率/刷新率不受支持（CDS_TEST 未通过）。");
            else
                Out("切换失败，错误码 " + r + "（" + DisplayManager.ErrorText(r) + "）");
            return 3;
        }

        private static int DoApply(string device, string sw, string sh, string sf)
        {
            int w, h, f;
            if (!int.TryParse(sw, out w) || !int.TryParse(sh, out h) || !int.TryParse(sf, out f))
            {
                Out("宽/高/刷新率必须是整数。");
                return 1;
            }
            int r = DisplayManager.SetMode(device, w, h, f);
            if (r == NativeMethods.DISP_CHANGE_SUCCESSFUL)
            {
                Out("切换成功：" + device + " -> " + w + "x" + h + " @" + f + "Hz");
                return 0;
            }
            if (r == NativeMethods.DISP_CHANGE_BADMODE)
                Out("切换失败：该分辨率/刷新率不受支持（CDS_TEST 未通过）。");
            else
                Out("切换失败，错误码 " + r + "（" + DisplayManager.ErrorText(r) + "）");
            return 3;
        }

        private static int DoMonitor(string instanceId, bool disable)
        {
            List<MonitorDevice> monitors = MonitorManager.GetMonitors();
            MonitorDevice target = MonitorManager.FindByInstanceId(monitors, instanceId);
            if (target == null)
            {
                Out("找不到监视器设备：" + instanceId);
                return 2;
            }

            // 安全兜底：禁止禁用最后一台处于启用状态的显示器
            if (disable && target.IsEnabled && MonitorManager.CountEnabled(monitors) <= 1)
            {
                Out("安全拦截：不能禁用最后一台处于启用状态的显示器（会导致全黑）。");
                return 5;
            }

            int cr = disable ? MonitorManager.Disable(instanceId) : MonitorManager.Enable(instanceId);
            if (cr == NativeMethods.CR_SUCCESS)
            {
                Out((disable ? "已禁用：" : "已启用：") + target.FriendlyName + "（" + instanceId + "）");
                return 0;
            }
            Out((disable ? "禁用失败：" : "启用失败：") + MonitorManager.ErrorText(cr));
            return 4;
        }

        private static StreamWriter nvApplyLog;

        private static void NvOut(string s)
        {
            Out(s);
            try { if (nvApplyLog != null) nvApplyLog.WriteLine(s); } catch { }
        }

        private static int DoNvidiaApply(string display, int scaling, int vibrance)
        {
            if (string.IsNullOrEmpty(display))
            {
                Out("缺少 --display 参数。");
                return 1;
            }
            try
            {
                nvApplyLog = new StreamWriter(
                    Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "nvidia-apply.log"),
                    false, Encoding.UTF8);
            }
            catch { nvApplyLog = null; }

            int exitCode = 0;
            try
            {
                if (!NvApi.Load())
                {
                    NvOut("NVAPI 不可用：" + NvApi.UnavailableReason);
                    return 4;
                }
                NvApi.DebugLog = true;

                // 先振动后缩放，各自独立执行，一个失败不影响另一个
                if (vibrance >= 0)
                {
                    int r;
                    try { r = NvApi.SetVibrance(display, vibrance); }
                    catch (Exception ex) { r = -1; NvOut("数字振动异常：" + ex.Message); }
                    if (r == NvApi.NVAPI_OK) NvOut("数字振动已设置为 " + vibrance + "（" + display + "）");
                    else { NvOut("数字振动设置失败：" + NvApi.ErrorString(r) + "（" + r + "）"); exitCode = 4; }
                }
                if (scaling >= 0)
                {
                    int r;
                    try { r = NvApi.SetScaling(display, scaling); }
                    catch (Exception ex) { r = -1; NvOut("缩放设置异常：" + ex.Message); }
                    if (r == NvApi.NVAPI_OK) NvOut("缩放模式已设置为 " + scaling + "（" + display + "）");
                    else { NvOut("缩放模式设置失败：" + NvApi.ErrorString(r) + "（" + r + "）"); exitCode = 4; }
                }
                NvApi.Unload();
            }
            finally
            {
                try { if (nvApplyLog != null) nvApplyLog.Close(); } catch { }
            }
            return exitCode;
        }

        // 只读诊断：DLL 加载 + 函数指针 + 当前缩放/数字振动读取，不调用任何 Set 接口
        private static int DoNvidiaProbe()
        {
            if (!NvApi.Load())
            {
                Out("NVAPI 不可用：" + NvApi.UnavailableReason);
                Out("已解析函数: " + string.Join(", ", NvApi.ResolvedFunctions.ToArray()));
                if (NvApi.MissingFunctions.Count > 0)
                    Out("缺失函数: " + string.Join(", ", NvApi.MissingFunctions.ToArray()));
                return 4;
            }
            Out("nvapi DLL 加载成功，NvAPI_Initialize OK");
            Out("已解析函数(" + NvApi.ResolvedFunctions.Count + "): " + string.Join(", ", NvApi.ResolvedFunctions.ToArray()));
            if (NvApi.MissingFunctions.Count > 0)
                Out("缺失函数: " + string.Join(", ", NvApi.MissingFunctions.ToArray()));

            List<DisplayInfo> displays = DisplayManager.GetDisplays();
            for (int i = 0; i < displays.Count; i++)
            {
                string name = displays[i].DeviceName;
                Out("");
                Out("显示器 " + name + "：");
                int scaling;
                int r = NvApi.GetScaling(name, out scaling);
                Out(r == NvApi.NVAPI_OK
                    ? "  当前缩放模式: " + scaling + "（" + ScalingName(scaling) + "）"
                    : "  缩放模式读取失败: " + NvApi.ErrorString(r) + "（" + r + "）");
                int cur, min, max, def;
                r = NvApi.GetVibrance(name, out cur, out min, out max, out def);
                Out(r == NvApi.NVAPI_OK
                    ? "  数字振动: 当前=" + cur + " 范围=[" + min + "," + max + "] 默认=" + def
                    : "  数字振动读取失败: " + NvApi.ErrorString(r) + "（" + r + "）");
            }
            NvApi.Unload();
            return 0;
        }

        private static string ScalingName(int scaling)
        {
            switch (scaling)
            {
                case 1: return "全屏（显示器缩放）";
                case 2: return "GPU缩放-全屏";
                case 3: return "GPU缩放-居中";
                case 5: return "GPU缩放-纵横比";
                case 6: return "纵横比（显示器缩放）";
                case 7: return "居中（显示器缩放）";
                case 8: return "GPU缩放-整数缩放";
                default: return "未知";
            }
        }

        // 一次性清理：注册表 DisplaySwitcher.* 项按 宽x高x刷新率 去重
        // （同组优先保留新格式 "@" 文案），同步清理 config.json 对应 profile，
        // 并把旧扁平数组格式统一迁移为新格式（Id 保持不变）。只动 DisplaySwitcher. 前缀子键。
        private static int DoCleanupRegistry()
        {
            List<RegistryManager.RegistryEntry> entries = RegistryManager.ListEntries();
            Out("清理前（共 " + entries.Count + " 项）：");
            PrintRegEntries(entries);

            Dictionary<string, RegistryManager.RegistryEntry> keep =
                new Dictionary<string, RegistryManager.RegistryEntry>(StringComparer.Ordinal);
            List<string> removeIds = new List<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                RegistryManager.RegistryEntry e = entries[i];
                int w, h, f;
                if (!RegistryManager.TryParseVerb(e.MUIVerb, out w, out h, out f))
                {
                    Out("  跳过（文案无法解析，保留）：" + e.Id + "  " + e.MUIVerb);
                    continue;
                }
                string key = w + "x" + h + "@" + f;
                RegistryManager.RegistryEntry existing;
                if (!keep.TryGetValue(key, out existing))
                {
                    keep[key] = e;
                }
                else if (!existing.IsNewFormat && e.IsNewFormat)
                {
                    // 新格式优先：删旧留新
                    removeIds.Add(existing.Id);
                    keep[key] = e;
                }
                else
                {
                    removeIds.Add(e.Id);
                }
            }

            if (removeIds.Count == 0)
            {
                Out("未发现重复项，注册表无需清理。");
            }
            else
            {
                for (int i = 0; i < removeIds.Count; i++)
                {
                    RegistryManager.RemoveEntry(removeIds[i]);
                    Out("已删除重复项：DisplaySwitcher." + removeIds[i]);
                }
            }

            try
            {
                List<DisplayProfile> profiles = ConfigStore.LoadProfiles();
                int before = profiles.Count;
                for (int i = profiles.Count - 1; i >= 0; i--)
                    if (removeIds.Contains(profiles[i].id)) profiles.RemoveAt(i);
                ConfigStore.SaveProfiles(profiles); // 统一写为新格式（旧扁平数组自动迁移，Id 不变）
                Out("config.json：profile " + before + " → " + profiles.Count + " 项（已统一为新格式）");
            }
            catch (Exception ex)
            {
                Out("config.json 处理失败：" + ex.Message);
            }

            List<RegistryManager.RegistryEntry> after = RegistryManager.ListEntries();
            Out("");
            Out("清理后（共 " + after.Count + " 项）：");
            PrintRegEntries(after);
            return 0;
        }

        private static void PrintRegEntries(List<RegistryManager.RegistryEntry> entries)
        {
            if (entries.Count == 0)
            {
                Out("  （无 DisplaySwitcher.* 菜单项）");
                return;
            }
            for (int i = 0; i < entries.Count; i++)
            {
                RegistryManager.RegistryEntry e = entries[i];
                int w, h, f;
                string parsed = RegistryManager.TryParseVerb(e.MUIVerb, out w, out h, out f)
                    ? w + "x" + h + "@" + f : "未解析";
                Out("  " + e.Id + "  \"" + e.MUIVerb + "\"  [" + parsed
                    + (e.IsNewFormat ? "，新格式" : "，旧格式") + "]");
            }
        }

        // ---------- 提权 ----------

        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RestartElevated(string[] args)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = Application.ExecutablePath;
            psi.Arguments = JoinArgs(args);
            psi.Verb = "runas";
            psi.UseShellExecute = true;
            Process.Start(psi);
        }

        private static string JoinArgs(string[] args)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(Quote(args[i]));
            }
            return sb.ToString();
        }

        private static string Quote(string s)
        {
            return "\"" + s.Replace("\"", "\\\"") + "\"";
        }

        private static bool HasFlag(string[] args, string flag)
        {
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string[] RemoveFlag(string[] args, string flag)
        {
            List<string> rest = new List<string>();
            for (int i = 0; i < args.Length; i++)
                if (!string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) rest.Add(args[i]);
            return rest.ToArray();
        }

        // ---------- 控制台输出（winexe 附加父控制台） ----------

        private static void AttachParentConsole()
        {
            consoleAttached = NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
            if (!consoleAttached) return;
            try
            {
                StreamWriter sw = new StreamWriter(Console.OpenStandardOutput());
                sw.AutoFlush = true;
                Console.SetOut(sw);
            }
            catch { }
        }

        private static void Out(string s)
        {
            Console.WriteLine(s);
        }

        private static void FinishConsole()
        {
            if (!consoleAttached) return;
            try
            {
                Console.Out.Flush();
                Console.WriteLine();
            }
            catch { }
            NativeMethods.FreeConsole();
        }
    }
}
