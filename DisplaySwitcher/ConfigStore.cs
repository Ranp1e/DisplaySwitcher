using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace DisplaySwitcher
{
    // config.json 读写，文件固定放在 EXE 同目录。
    // 兼容：读取时自动识别旧格式（扁平数组）并转换为新格式；写入统一用新格式。
    public static class ConfigStore
    {
        public static string ConfigPath
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "config.json");
            }
        }

        // 归一化读取整个配置根：旧扁平数组自动转为新格式，缺失字段补默认值
        private static ConfigRoot LoadRoot()
        {
            ConfigRoot root = new ConfigRoot();
            if (!File.Exists(ConfigPath)) return root;
            string json = File.ReadAllText(ConfigPath);
            if (string.IsNullOrEmpty(json) || json.Trim().Length == 0) return root;

            JavaScriptSerializer ser = new JavaScriptSerializer();
            json = json.Trim();
            if (json.StartsWith("["))
            {
                // 旧格式：扁平数组
                List<LegacyDisplayConfig> legacy = ser.Deserialize<List<LegacyDisplayConfig>>(json);
                if (legacy != null)
                    for (int i = 0; i < legacy.Count; i++)
                        if (legacy[i] != null) root.profiles.Add(legacy[i].ToProfile());
                return root;
            }

            ConfigRoot parsed = ser.Deserialize<ConfigRoot>(json);
            if (parsed == null) return root;
            if (parsed.profiles != null) root.profiles = parsed.profiles;
            if (parsed.disabledMonitors != null) root.disabledMonitors = parsed.disabledMonitors;
            if (parsed.nvidiaSettings != null) root.nvidiaSettings = parsed.nvidiaSettings;
            if (parsed.amdSettings != null) root.amdSettings = parsed.amdSettings;
            return root;
        }

        private static void SaveRoot(ConfigRoot root)
        {
            if (root.disabledMonitors == null) root.disabledMonitors = new List<string>();
            JavaScriptSerializer ser = new JavaScriptSerializer();
            File.WriteAllText(ConfigPath, ser.Serialize(root));
        }

        public static List<DisplayProfile> LoadProfiles()
        {
            return LoadRoot().profiles;
        }

        public static void SaveProfiles(List<DisplayProfile> profiles)
        {
            ConfigRoot root = LoadRoot();
            root.profiles = profiles ?? new List<DisplayProfile>();
            SaveRoot(root);
        }

        public static NvidiaSettings LoadNvidiaSettings()
        {
            ConfigRoot root = LoadRoot();
            return root.nvidiaSettings ?? new NvidiaSettings();
        }

        public static void SaveNvidiaSettings(NvidiaSettings settings)
        {
            ConfigRoot root = LoadRoot();
            root.nvidiaSettings = settings;
            SaveRoot(root);
        }

        public static AmdSettings LoadAmdSettings()
        {
            ConfigRoot root = LoadRoot();
            return root.amdSettings ?? new AmdSettings();
        }

        public static void SaveAmdSettings(AmdSettings settings)
        {
            ConfigRoot root = LoadRoot();
            root.amdSettings = settings;
            SaveRoot(root);
        }

        public static DisplayProfile FindById(string id)
        {
            List<DisplayProfile> all = LoadProfiles();
            for (int i = 0; i < all.Count; i++)
                if (string.Equals(all[i].id, id, StringComparison.OrdinalIgnoreCase))
                    return all[i];
            return null;
        }

        public static void Clear()
        {
            SaveProfiles(new List<DisplayProfile>());
        }
    }
}
