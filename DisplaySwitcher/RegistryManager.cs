using System;
using Microsoft.Win32;

namespace DisplaySwitcher
{
    // 桌面右键菜单注册表注入（HKCU，无需管理员权限）
    public static class RegistryManager
    {
        private const string RootPath = @"Software\Classes\DesktopBackground\Shell";
        private const string KeyPrefix = "DisplaySwitcher.";

        public static void AddMenuEntry(DisplayConfig cfg, string exePath)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RootPath + "\\" + KeyPrefix + cfg.Id))
            {
                if (key == null) throw new InvalidOperationException("无法创建注册表项");
                key.SetValue("MUIVerb", cfg.MenuName);
                key.SetValue("Icon", exePath);
                using (RegistryKey cmd = key.CreateSubKey("command"))
                {
                    cmd.SetValue("", "\"" + exePath + "\" --switch \"" + cfg.Id + "\" --elevated");
                }
            }
        }

        // 删除本软件创建的所有菜单项
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
