using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace DisplaySwitcher
{
    // config.json 读写，文件固定放在 EXE 同目录
    public static class ConfigStore
    {
        public static string ConfigPath
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "config.json");
            }
        }

        public static List<DisplayConfig> Load()
        {
            if (!File.Exists(ConfigPath)) return new List<DisplayConfig>();
            string json = File.ReadAllText(ConfigPath);
            if (string.IsNullOrEmpty(json) || json.Trim().Length == 0) return new List<DisplayConfig>();
            JavaScriptSerializer ser = new JavaScriptSerializer();
            List<DisplayConfig> list = ser.Deserialize<List<DisplayConfig>>(json);
            return list ?? new List<DisplayConfig>();
        }

        public static void Save(List<DisplayConfig> list)
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            File.WriteAllText(ConfigPath, ser.Serialize(list));
        }

        public static DisplayConfig FindById(string id)
        {
            List<DisplayConfig> all = Load();
            for (int i = 0; i < all.Count; i++)
                if (string.Equals(all[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return all[i];
            return null;
        }

        public static void Clear()
        {
            Save(new List<DisplayConfig>());
        }
    }
}
