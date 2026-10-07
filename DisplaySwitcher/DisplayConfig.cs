using System;

namespace DisplaySwitcher
{
    // config.json 中每一项的模型。FriendlyName 为附加字段，用于 DeviceName 失效时按友好名兜底匹配。
    public class DisplayConfig
    {
        public string Id { get; set; }
        public string DeviceName { get; set; }
        public string FriendlyName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Frequency { get; set; }
        public string MenuName { get; set; }
    }
}
