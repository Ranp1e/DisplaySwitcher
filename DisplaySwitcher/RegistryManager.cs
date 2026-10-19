using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace DisplaySwitcher
{
    // 桌面右键菜单注册表注入（HKCU，无需管理员权限）
    public static class RegistryManager
    {
        private const string RootPath = @"Software\Classes\DesktopBackground\Shell";
        private const string KeyPrefix = "DisplaySwitcher.";

        public static void AddMenuEntry(DisplayProfile profile, string exePath)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RootPath + "\\" + KeyPrefix + profile.id))
            {
                if (key == null) throw new InvalidOperationException("无法创建注册表项");
                key.SetValue("MUIVerb", profile.MenuTitle());
                key.SetValue("Icon", exePath);
                using (RegistryKey cmd = key.CreateSubKey("command"))
                {
                    // 切换分辨率是当前用户设置，无需管理员权限，不带 --elevated
                    cmd.SetValue("", "\"" + exePath + "\" --switch \"" + profile.id + "\"");
                }
            }
        }

        // 启动时自愈菜单项：旧命令带 --elevated 或 EXE 路径已变动时重写 command，
        // 并把 Icon 同步为当前 EXE 路径。返回修正的条目数。
        public static int MigrateElevatedCommands(string exePath)
        {
            int migrated = 0;
            using (RegistryKey shell = Registry.CurrentUser.OpenSubKey(RootPath, true))
            {
                if (shell == null) return 0;
                string[] names = shell.GetSubKeyNames();
                for (int i = 0; i < names.Length; i++)
                {
                    if (!names[i].StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    using (RegistryKey key = shell.OpenSubKey(names[i], true))
                    {
                        if (key == null) continue;
                        string id = names[i].Substring(KeyPrefix.Length);
                        string expected = "\"" + exePath + "\" --switch \"" + id + "\"";
                        bool fixedItem = false;
                        using (RegistryKey cmd = key.OpenSubKey("command", true))
                        {
                            if (cmd != null)
                            {
                                string command = cmd.GetValue("") as string;
                                if (command != expected)
                                {
                                    cmd.SetValue("", expected);
                                    fixedItem = true;
                                }
                            }
                        }
                        string icon = key.GetValue("Icon") as string;
                        if (icon != exePath)
                        {
                            key.SetValue("Icon", exePath);
                            fixedItem = true;
                        }
                        if (fixedItem) migrated++;
                    }
                }
            }
            return migrated;
        }

        public static int AddMenuEntries(List<DisplayProfile> profiles, string exePath)
        {
            int added = 0;
            try
            {
                for (int i = 0; i < profiles.Count; i++)
                {
                    AddMenuEntry(profiles[i], exePath);
                    VerifyEntry(profiles[i].id);
                    added++;
                }
            }
            catch
            {
                // 回滚本次已写入的项，避免残留半成品菜单
                for (int i = 0; i < added; i++) RemoveEntry(profiles[i].id);
                throw;
            }
            return added;
        }

        // 写入后立即读回校验：确认注册表项和命令真实存在（防安全软件静默拦截）
        private static void VerifyEntry(string id)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RootPath + "\\" + KeyPrefix + id, false))
            {
                if (key == null)
                    throw new InvalidOperationException("注册表写入后读回失败，可能被安全软件拦截");
                using (RegistryKey cmd = key.OpenSubKey("command", false))
                {
                    string command = cmd != null ? cmd.GetValue("") as string : null;
                    if (string.IsNullOrEmpty(command) || command.IndexOf(id, StringComparison.OrdinalIgnoreCase) < 0)
                        throw new InvalidOperationException("注册表命令读回校验失败，可能被安全软件拦截");
                }
            }
        }

        // 删除单个菜单项（不存在时静默忽略）
        public static void RemoveEntry(string id)
        {
            using (RegistryKey shell = Registry.CurrentUser.OpenSubKey(RootPath, true))
            {
                if (shell == null) return;
                shell.DeleteSubKeyTree(KeyPrefix + id, false);
            }
        }

        // 注册表中实际存在的菜单项
        public class RegistryEntry
        {
            public string Id;        // 子键名去掉 DisplaySwitcher. 前缀
            public string MUIVerb;   // 菜单文案
            public string Command;   // command 子键默认值
            public bool IsNewFormat; // MUIVerb 含 "@"（新格式文案）
        }

        // 列出所有 DisplaySwitcher. 前缀菜单项
        public static List<RegistryEntry> ListEntries()
        {
            List<RegistryEntry> list = new List<RegistryEntry>();
            using (RegistryKey shell = Registry.CurrentUser.OpenSubKey(RootPath, false))
            {
                if (shell == null) return list;
                string[] names = shell.GetSubKeyNames();
                for (int i = 0; i < names.Length; i++)
                {
                    if (!names[i].StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    using (RegistryKey key = shell.OpenSubKey(names[i]))
                    {
                        if (key == null) continue;
                        RegistryEntry e = new RegistryEntry();
                        e.Id = names[i].Substring(KeyPrefix.Length);
                        e.MUIVerb = key.GetValue("MUIVerb") as string ?? "";
                        e.IsNewFormat = e.MUIVerb.IndexOf('@') >= 0;
                        using (RegistryKey cmd = key.OpenSubKey("command"))
                        {
                            e.Command = cmd != null ? cmd.GetValue("") as string : null;
                        }
                        list.Add(e);
                    }
                }
            }
            return list;
        }

        // 解析菜单文案中的模式参数："切换到 1568x1080 240Hz"（旧）或 "切换到 1568x1080 @240Hz"（新）
        public static bool TryParseVerb(string verb, out int w, out int h, out int f)
        {
            w = 0; h = 0; f = 0;
            if (string.IsNullOrEmpty(verb)) return false;
            int sp = verb.Trim().IndexOf(' ');
            if (sp < 0) return false;
            string rest = verb.Trim().Substring(sp).Trim();
            string[] parts = rest.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;
            string[] wh = parts[0].ToLowerInvariant().Replace("×", "x").Split('x');
            if (wh.Length != 2) return false;
            if (!int.TryParse(wh[0], out w) || !int.TryParse(wh[1], out h)) return false;
            string fs = parts[1].Replace("@", "").Replace("Hz", "").Replace("hz", "").Trim();
            if (!int.TryParse(fs, out f)) return false;
            return w > 0 && h > 0 && f > 0;
        }

        // 删除本软件创建的所有菜单项（DisplaySwitcher. 前缀子键）
        public static int RemoveAll()
        {
            int removed = 0;
            using (RegistryKey shell = Registry.CurrentUser.OpenSubKey(RootPath, true))
            {
                if (shell == null) return 0;
                string[] names = shell.GetSubKeyNames();
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i].StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        shell.DeleteSubKeyTree(names[i]);
                        removed++;
                    }
                }
            }
            return removed;
        }
    }
}
