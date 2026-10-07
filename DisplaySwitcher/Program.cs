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
                        // 用户拒绝 UAC 时降级：仍直接尝试切换（通常无需管理员）
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

                Out("未知参数。用法：");
                Out("  DisplaySwitcher.exe                                   打开配置界面");
                Out("  DisplaySwitcher.exe --list                            列出所有显示器");
                Out("  DisplaySwitcher.exe --switch \"ID\" [--elevated]        按配置切换");
                Out("  DisplaySwitcher.exe --apply \"\\\\.\\DISPLAYx\" 宽 高 刷新率 [--elevated]");
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
                return 0;
            }
            for (int i = 0; i < list.Count; i++)
            {
                DisplayInfo d = list[i];
                string friendly = string.IsNullOrEmpty(d.MonitorName) ? d.AdapterString : d.MonitorName;
                Out(d.DeviceName + "  " + friendly + "  当前: "
                    + d.CurrentWidth + "x" + d.CurrentHeight + " @" + d.CurrentFrequency + "Hz");
            }
            return 0;
        }

        private static int DoSwitch(string id)
        {
            DisplayConfig cfg;
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

            string device = cfg.DeviceName;
            if (!DisplayManager.DeviceExists(device))
            {
                string alt = DisplayManager.FindDeviceByFriendlyName(cfg.FriendlyName);
                if (alt == null)
                {
                    Out("显示器 " + device + " 当前不存在，且无法按友好名匹配。");
                    return 2;
                }
                Out("设备名 " + device + " 已失效，按友好名匹配到 " + alt);
                device = alt;
            }

            int r = DisplayManager.SetMode(device, cfg.Width, cfg.Height, cfg.Frequency);
            if (r == NativeMethods.DISP_CHANGE_SUCCESSFUL)
            {
                Out("切换成功：" + device + " -> " + cfg.Width + "x" + cfg.Height + " @" + cfg.Frequency + "Hz");
                return 0;
            }
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
            Out("切换失败，错误码 " + r + "（" + DisplayManager.ErrorText(r) + "）");
            return 3;
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
