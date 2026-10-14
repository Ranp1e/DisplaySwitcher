using System;
using System.Collections.Generic;

namespace DisplaySwitcher
{
    // v2.1.0 新配置结构。属性名用 camelCase，使 JavaScriptSerializer
    // 序列化输出与规格中的 JSON 键名一致（反序列化大小写不敏感，读旧键也没问题）。
    public class DisplayProfile
    {
        public string id { get; set; }
        public string deviceName { get; set; }
        public string friendlyName { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public int refresh { get; set; }

        public string MenuTitle()
        {
            return "切换到 " + width + "x" + height + " @" + refresh + "Hz";
        }

        public string Summary()
        {
            return width + "x" + height + "@" + refresh;
        }
    }

    public class ConfigRoot
    {
        public List<DisplayProfile> profiles { get; set; }
        public List<string> disabledMonitors { get; set; }
        public NvidiaSettings nvidiaSettings { get; set; }

        public ConfigRoot()
        {
            profiles = new List<DisplayProfile>();
            disabledMonitors = new List<string>();
            nvidiaSettings = null;
        }
    }

    // NVIDIA 显示增强设置（缺失时读默认值，向后兼容）
    public class NvidiaSettings
    {
        public int scalingMode { get; set; }      // NV_SCALING：0=未设置 1=显示器 2=GPU全屏 3=居中 5=纵横比 8=整数
        public int digitalVibrance { get; set; }  // -1=未设置

        public NvidiaSettings()
        {
            scalingMode = 0;
            digitalVibrance = -1;
        }
    }

    // v1/v2.0 旧格式（扁平数组）反序列化模型，仅用于兼容读取。
    // 旧格式：[{"Id":"1b07911d","DeviceName":"...","Width":1568,"Height":1080,"Frequency":240,"MenuName":"..."}]
    public class LegacyDisplayConfig
    {
        public string Id { get; set; }
        public string DeviceName { get; set; }
        public string FriendlyName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Frequency { get; set; }
        public string MenuName { get; set; }

        public DisplayProfile ToProfile()
        {
            DisplayProfile p = new DisplayProfile();
            p.id = Id;
            p.deviceName = DeviceName;
            p.friendlyName = FriendlyName;
            p.width = Width;
            p.height = Height;
            p.refresh = Frequency;
            return p;
        }
    }
}
