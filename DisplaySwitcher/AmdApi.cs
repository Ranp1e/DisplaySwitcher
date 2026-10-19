using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DisplaySwitcher
{
    // ADL（AMD Display Library）动态加载封装：LoadLibrary("atiadlxx.dll")
    // （32 位回退 atiadlxy.dll，AMD 驱动安装后即在 System32），普通 C 导出，
    // 直接 GetProcAddress 按名字解析，无需打包任何 DLL。
    // 函数签名与结构体布局按 AMD 官方 ADL SDK 头文件（GPUOpen display-library，
    // adl_structures.h / adl_defines.h）手工封送。
    internal static class AmdApi
    {
        // ADL 通用错误码（adl_defines.h）
        public const int ADL_OK = 0;
        public const int ADL_ERR = -1;
        public const int ADL_ERR_NOT_SUPPORTED = -8;

        // ADL_DISPLAY_COLOR_*（adl_defines.h，位标志）
        private const int COLOR_SATURATION = 1 << 2;

        // 应用内统一的缩放模式逻辑码（与 NvApi 的显示缩放两项对齐）：
        // 纵横比 = GPU 缩放开 + 保持纵横比开；全屏 = GPU 缩放开 + 保持纵横比关
        public const int SCALING_ASPECT = 6;
        public const int SCALING_FULLSCREEN = 1;

        // ADLDisplayInfo 固定 552 字节：displayID(16) + controllerIndex(4)
        // + strDisplayName[256] + strDisplayManufacturerName[256] + 5×int
        private const int DISPLAY_INFO_SIZE = 552;
        private const int OFF_DI_LOGICAL_INDEX = 0;   // displayID.iDisplayLogicalIndex
        private const int OFF_DI_NAME = 20;           // strDisplayName（EDID 名）
        private const int OFF_DI_INFO_VALUE = 548;    // iDisplayInfoValue
        private const int DISPLAY_CONNECTED = 0x1;    // ADL_DISPLAY_DISPLAYINFO_DISPLAYCONNECTED

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr AdlMallocDelegate(int size);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MainControlCreateDelegate(AdlMallocDelegate callback, int enumConnectedAdapters, out IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MainControlDestroyDelegate(IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int AdapterNumberOfAdaptersDelegate(IntPtr context, out int count);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int AdapterActiveDelegate(IntPtr context, int adapterIndex, out int status);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int DisplayInfoGetDelegate(IntPtr context, int adapterIndex, ref int numDisplays, out IntPtr info, int forceDetect);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int FeatureGetDelegate(IntPtr context, int adapterIndex, int displayIndex, out int support, out int current, out int def);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int FeatureSetDelegate(IntPtr context, int adapterIndex, int displayIndex, int current);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ColorGetDelegate(IntPtr context, int adapterIndex, int displayIndex, int colorType, out int current, out int def, out int min, out int max, out int step);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ColorSetDelegate(IntPtr context, int adapterIndex, int displayIndex, int colorType, int current);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ColorCapsGetDelegate(IntPtr context, int adapterIndex, int displayIndex, out int caps, out int valids);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int CustomizedModeValidateDelegate(IntPtr context, int adapterIndex, int displayIndex, AdlCustomMode mode, out int valid);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int CustomizedModeAddDelegate(IntPtr context, int adapterIndex, int displayIndex, AdlCustomMode mode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int CustomizedModeDeleteDelegate(IntPtr context, int adapterIndex, int displayIndex, int index);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int CustomizedModeListNumGetDelegate(IntPtr context, int adapterIndex, int displayIndex, out int num);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int CustomizedModeListGetDelegate(IntPtr context, int adapterIndex, int displayIndex, IntPtr list, int bufSize);

        // ADLCustomMode（adl_structures.h）：24 字节，Add/Validate 按值传递
        [StructLayout(LayoutKind.Sequential)]
        private struct AdlCustomMode
        {
            public int Flags;
            public int ModeWidth;
            public int ModeHeight;
            public int BaseModeWidth;
            public int BaseModeHeight;
            public int RefreshRate;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        public static bool Available;
        public static string UnavailableReason = "";

        private static bool attempted;
        private static IntPtr context;
        // ADL 分配回调必须常驻（驱动经它分配 DisplayInfo 缓冲，用完由我们 FreeHGlobal）
        private static readonly AdlMallocDelegate mallocCallback = new AdlMallocDelegate(AdlMalloc);

        private static MainControlDestroyDelegate mainControlDestroy;
        private static AdapterNumberOfAdaptersDelegate adapterNumberOfAdapters;
        private static AdapterActiveDelegate adapterActive;
        private static DisplayInfoGetDelegate displayInfoGet;
        private static FeatureGetDelegate imageExpansionGet;
        private static FeatureSetDelegate imageExpansionSet;
        private static FeatureGetDelegate preservedAspectRatioGet;
        private static FeatureSetDelegate preservedAspectRatioSet;
        private static ColorGetDelegate colorGet;
        private static ColorSetDelegate colorSet;
        private static ColorCapsGetDelegate colorCapsGet;
        private static CustomizedModeValidateDelegate customizedModeValidate;
        private static CustomizedModeAddDelegate customizedModeAdd;
        private static CustomizedModeDeleteDelegate customizedModeDelete;
        private static CustomizedModeListNumGetDelegate customizedModeListNumGet;
        private static CustomizedModeListGetDelegate customizedModeListGet;

        private static IntPtr AdlMalloc(int size)
        {
            return Marshal.AllocHGlobal(size);
        }

        public static bool Load()
        {
            if (attempted) return Available;
            attempted = true;

            IntPtr lib = LoadLibrary("atiadlxx.dll");
            if (lib == IntPtr.Zero) lib = LoadLibrary("atiadlxy.dll");
            if (lib == IntPtr.Zero)
            {
                UnavailableReason = "atiadlxx.dll 未找到（无 AMD 显卡或驱动未安装）";
                return false;
            }

            MainControlCreateDelegate create = Resolve<MainControlCreateDelegate>(lib, "ADL2_Main_Control_Create");
            mainControlDestroy = Resolve<MainControlDestroyDelegate>(lib, "ADL2_Main_Control_Destroy");
            adapterNumberOfAdapters = Resolve<AdapterNumberOfAdaptersDelegate>(lib, "ADL2_Adapter_NumberOfAdapters_Get");
            adapterActive = Resolve<AdapterActiveDelegate>(lib, "ADL2_Adapter_Active_Get");
            displayInfoGet = Resolve<DisplayInfoGetDelegate>(lib, "ADL2_Display_DisplayInfo_Get");
            imageExpansionGet = Resolve<FeatureGetDelegate>(lib, "ADL2_Display_ImageExpansion_Get");
            imageExpansionSet = Resolve<FeatureSetDelegate>(lib, "ADL2_Display_ImageExpansion_Set");
            preservedAspectRatioGet = Resolve<FeatureGetDelegate>(lib, "ADL2_Display_PreservedAspectRatio_Get");
            preservedAspectRatioSet = Resolve<FeatureSetDelegate>(lib, "ADL2_Display_PreservedAspectRatio_Set");
            colorGet = Resolve<ColorGetDelegate>(lib, "ADL2_Display_Color_Get");
            colorSet = Resolve<ColorSetDelegate>(lib, "ADL2_Display_Color_Set");
            colorCapsGet = Resolve<ColorCapsGetDelegate>(lib, "ADL2_Display_ColorCaps_Get");
            customizedModeValidate = Resolve<CustomizedModeValidateDelegate>(lib, "ADL2_Display_CustomizedMode_Validate");
            customizedModeAdd = Resolve<CustomizedModeAddDelegate>(lib, "ADL2_Display_CustomizedMode_Add");
            customizedModeDelete = Resolve<CustomizedModeDeleteDelegate>(lib, "ADL2_Display_CustomizedMode_Delete");
            customizedModeListNumGet = Resolve<CustomizedModeListNumGetDelegate>(lib, "ADL2_Display_CustomizedModeListNum_Get");
            customizedModeListGet = Resolve<CustomizedModeListGetDelegate>(lib, "ADL2_Display_CustomizedModeList_Get");

            if (create == null || adapterNumberOfAdapters == null || displayInfoGet == null)
            {
                UnavailableReason = "ADL 关键函数解析失败";
                return false;
            }

            int r = create(mallocCallback, 1, out context);
            if (r != ADL_OK)
            {
                UnavailableReason = "ADL2_Main_Control_Create 返回 " + r;
                return false;
            }
            int count;
            r = adapterNumberOfAdapters(context, out count);
            if (r != ADL_OK || count <= 0)
            {
                UnavailableReason = "未检测到 AMD 适配器";
                return false;
            }
            Available = true;
            return true;
        }

        private static T Resolve<T>(IntPtr lib, string name) where T : class
        {
            IntPtr ptr = GetProcAddress(lib, name);
            if (ptr == IntPtr.Zero) return null;
            return (T)(object)Marshal.GetDelegateForFunctionPointer(ptr, typeof(T));
        }

        public static void Unload()
        {
            if (Available && mainControlDestroy != null && context != IntPtr.Zero)
            {
                try { mainControlDestroy(context); } catch { }
                context = IntPtr.Zero;
            }
        }

        public static string ErrorString(int code)
        {
            switch (code)
            {
                case ADL_OK: return "成功";
                case ADL_ERR: return "一般性错误";
                case -2: return "ADL 未初始化";
                case -3: return "参数无效";
                case -5: return "适配器/显示器索引无效";
                case -6: return "无显示设备";
                case ADL_ERR_NOT_SUPPORTED: return "该显示器不支持此功能";
                case -11: return "内存回调无效";
                default: return "ADL 错误 " + code;
            }
        }

        // 第一个已连接的 AMD 显示器（适配器索引 + 逻辑显示器索引 + EDID 名）。
        // 按设计：AMD 面板自动作用于 AMD 卡上第一个已连接显示器。
        public static int FindDisplay(out int adapterIndex, out int displayIndex, out string displayName)
        {
            adapterIndex = -1; displayIndex = -1; displayName = "";
            if (!Available) return ADL_ERR;

            int count;
            int r = adapterNumberOfAdapters(context, out count);
            if (r != ADL_OK) return r;

            for (int a = 0; a < count; a++)
            {
                if (adapterActive != null)
                {
                    int status;
                    if (adapterActive(context, a, out status) == ADL_OK && status != 1) continue;
                }
                int numDisplays = 0;
                IntPtr info = IntPtr.Zero;
                r = displayInfoGet(context, a, ref numDisplays, out info, 0);
                if (r != ADL_OK || numDisplays <= 0 || info == IntPtr.Zero) continue;
                try
                {
                    for (int d = 0; d < numDisplays; d++)
                    {
                        IntPtr item = new IntPtr(info.ToInt64() + (long)d * DISPLAY_INFO_SIZE);
                        int infoValue = Marshal.ReadInt32(item, OFF_DI_INFO_VALUE);
                        if ((infoValue & DISPLAY_CONNECTED) == 0) continue;
                        adapterIndex = a;
                        displayIndex = Marshal.ReadInt32(item, OFF_DI_LOGICAL_INDEX);
                        displayName = ReadAnsiString(item, OFF_DI_NAME, 256);
                        return ADL_OK;
                    }
                }
                finally { Marshal.FreeHGlobal(info); }
            }
            return -6; // 无显示设备
        }

        private static string ReadAnsiString(IntPtr buf, int offset, int maxLen)
        {
            byte[] bytes = new byte[maxLen];
            Marshal.Copy(new IntPtr(buf.ToInt64() + offset), bytes, 0, maxLen);
            int end = Array.IndexOf(bytes, (byte)0);
            if (end < 0) end = maxLen;
            return Encoding.ASCII.GetString(bytes, 0, end).Trim();
        }

        // ---------- 缩放模式（GPU 缩放 + 保持纵横比 组合） ----------

        public static int GetScaling(out int scaling)
        {
            scaling = -1;
            if (imageExpansionGet == null || preservedAspectRatioGet == null) return ADL_ERR;
            int adapter, display;
            string name;
            int r = FindDisplay(out adapter, out display, out name);
            if (r != ADL_OK) return r;

            int sup, expansion, def;
            r = imageExpansionGet(context, adapter, display, out sup, out expansion, out def);
            if (r != ADL_OK) return r;
            if (sup != 1) return ADL_ERR_NOT_SUPPORTED;
            int sup2, par, def2;
            r = preservedAspectRatioGet(context, adapter, display, out sup2, out par, out def2);
            if (r != ADL_OK) return r;
            scaling = par == 1 ? SCALING_ASPECT : SCALING_FULLSCREEN;
            return ADL_OK;
        }

        public static int SetScaling(int scaling)
        {
            if (imageExpansionSet == null || preservedAspectRatioSet == null) return ADL_ERR;
            int adapter, display;
            string name;
            int r = FindDisplay(out adapter, out display, out name);
            if (r != ADL_OK) return r;

            int par = scaling == SCALING_ASPECT ? 1 : 0;
            r = imageExpansionSet(context, adapter, display, 1); // 始终开 GPU 缩放
            if (r != ADL_OK) return r;
            return preservedAspectRatioSet(context, adapter, display, par);
        }

        // ---------- 饱和度（对应 NVIDIA 数字振动） ----------

        public static int GetSaturation(out int current, out int min, out int max, out int def)
        {
            current = 0; min = 0; max = 200; def = 100;
            if (colorGet == null) return ADL_ERR;
            int adapter, display;
            string name;
            int r = FindDisplay(out adapter, out display, out name);
            if (r != ADL_OK) return r;

            if (colorCapsGet != null)
            {
                int caps, valids;
                if (colorCapsGet(context, adapter, display, out caps, out valids) == ADL_OK
                    && (valids & COLOR_SATURATION) == 0)
                    return ADL_ERR_NOT_SUPPORTED;
            }
            int step;
            return colorGet(context, adapter, display, COLOR_SATURATION, out current, out def, out min, out max, out step);
        }

        public static int SetSaturation(int value)
        {
            if (colorSet == null) return ADL_ERR;
            int adapter, display;
            string name;
            int r = FindDisplay(out adapter, out display, out name);
            if (r != ADL_OK) return r;

            // 先读出范围做钳制
            if (colorGet != null)
            {
                int cur, def, min, max, step;
                if (colorGet(context, adapter, display, COLOR_SATURATION, out cur, out def, out min, out max, out step) == ADL_OK)
                {
                    if (value < min) value = min;
                    if (value > max) value = max;
                }
            }
            return colorSet(context, adapter, display, COLOR_SATURATION, value);
        }

        // ---------- 自定义分辨率（CustomizedMode，驱动自算时序） ----------

        // 创建 A 卡自定义分辨率：先 Validate 校验，通过才 Add
        public static int CreateCustomMode(int w, int h, int refresh)
        {
            if (customizedModeValidate == null || customizedModeAdd == null) return ADL_ERR;
            int adapter, display;
            string name;
            int r = FindDisplay(out adapter, out display, out name);
            if (r != ADL_OK) return r;

            AdlCustomMode mode = new AdlCustomMode();
            mode.Flags = 0;
            mode.ModeWidth = w;
            mode.ModeHeight = h;
            mode.BaseModeWidth = w;
            mode.BaseModeHeight = h;
            mode.RefreshRate = refresh;

            int valid;
            r = customizedModeValidate(context, adapter, display, mode, out valid);
            if (r != ADL_OK) return r;
            if (valid != 1) return ADL_ERR_NOT_SUPPORTED;
            return customizedModeAdd(context, adapter, display, mode);
        }

        // 删除 A 卡自定义分辨率：先枚举列表找匹配项的索引再删
        public static int DeleteCustomMode(int w, int h, int refresh)
        {
            if (customizedModeDelete == null || customizedModeListNumGet == null || customizedModeListGet == null)
                return ADL_ERR;
            int adapter, display;
            string name;
            int r = FindDisplay(out adapter, out display, out name);
            if (r != ADL_OK) return r;

            int num;
            r = customizedModeListNumGet(context, adapter, display, out num);
            if (r != ADL_OK) return r;
            if (num <= 0) return -6; // 无自定义模式

            IntPtr list = Marshal.AllocHGlobal(num * 24);
            try
            {
                r = customizedModeListGet(context, adapter, display, list, num * 24);
                if (r != ADL_OK) return r;
                for (int i = 0; i < num; i++)
                {
                    IntPtr item = new IntPtr(list.ToInt64() + (long)i * 24);
                    int mw = Marshal.ReadInt32(item, 4);
                    int mh = Marshal.ReadInt32(item, 8);
                    int rr = Marshal.ReadInt32(item, 20);
                    if (mw == w && mh == h && rr == refresh)
                        return customizedModeDelete(context, adapter, display, i);
                }
                return -6; // 未找到匹配项
            }
            finally { Marshal.FreeHGlobal(list); }
        }

        // 诊断用：列出所有已连接显示器及其缩放/饱和度读数
        public static int ProbeDisplays(List<string> lines)
        {
            int adapter, display;
            string name;
            int r = FindDisplay(out adapter, out display, out name);
            if (r != ADL_OK)
            {
                lines.Add("未找到已连接的 AMD 显示器：" + ErrorString(r));
                return r;
            }
            lines.Add("目标显示器: 适配器 " + adapter + " / 显示器 " + display + "（" + name + "）");

            int scaling;
            r = GetScaling(out scaling);
            lines.Add(r == ADL_OK
                ? "  缩放模式: " + (scaling == SCALING_ASPECT ? "纵横比" : "全屏")
                : "  缩放模式读取失败: " + ErrorString(r) + "（" + r + "）");

            int cur, min, max, def;
            r = GetSaturation(out cur, out min, out max, out def);
            lines.Add(r == ADL_OK
                ? "  饱和度: 当前=" + cur + " 范围=[" + min + "," + max + "] 默认=" + def
                : "  饱和度读取失败: " + ErrorString(r) + "（" + r + "）");

            // 已创建的自定义模式
            if (customizedModeListNumGet != null && customizedModeListGet != null)
            {
                int num;
                if (customizedModeListNumGet(context, adapter, display, out num) == ADL_OK && num > 0)
                {
                    IntPtr list = Marshal.AllocHGlobal(num * 24);
                    try
                    {
                        if (customizedModeListGet(context, adapter, display, list, num * 24) == ADL_OK)
                        {
                            lines.Add("  自定义分辨率（共 " + num + " 项）：");
                            for (int i = 0; i < num; i++)
                            {
                                IntPtr item = new IntPtr(list.ToInt64() + (long)i * 24);
                                lines.Add("    [" + i + "] " + Marshal.ReadInt32(item, 4) + "x" + Marshal.ReadInt32(item, 8)
                                    + " @" + Marshal.ReadInt32(item, 20) + "Hz flags=0x" + Marshal.ReadInt32(item, 0).ToString("X"));
                            }
                        }
                    }
                    finally { Marshal.FreeHGlobal(list); }
                }
                else
                {
                    lines.Add("  自定义分辨率：无");
                }
            }
            return ADL_OK;
        }
    }
}
