using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DisplaySwitcher
{
    // NVAPI 动态加载封装：LoadLibrary("nvapi64.dll")（32 位回退 nvapi.dll）
    // + GetProcAddress("nvapi_QueryInterface") 拿函数指针。不打包任何 DLL。
    // 函数 ID 来自 NVIDIA 官方开源 nvapi_interface.h（github.com/NVIDIA/nvapi）；
    // DVC（数字振动）两个 ID 不在开源接口表中，采用社区广泛使用的固定值。
    // 结构体布局按官方 nvapi.h 的 versioned struct 惯例手工封送
    // （ver 字段 = sizeof | (version<<16)）。
    internal static class NvApi
    {
        private const uint ID_INITIALIZE = 0x0150E828;
        private const uint ID_UNLOAD = 0xD22BDD7E;
        private const uint ID_GET_ERROR_MESSAGE = 0x6C2D048C;
        private const uint ID_ENUM_DISPLAY_HANDLE = 0x9ABDD40D;
        private const uint ID_GET_DISPLAY_NAME = 0x22A78B05;
        private const uint ID_GET_OUTPUT_ID = 0xD995937E;
        private const uint ID_GET_DISPLAY_ID_BY_NAME = 0xAE457190;
        private const uint ID_GET_DISPLAY_CONFIG = 0x11ABCCF8;
        private const uint ID_SET_DISPLAY_CONFIG = 0x5D8CF8DE;
        private const uint ID_GET_DVC_INFO_EX = 0x0E45002D;
        private const uint ID_SET_DVC_LEVEL_EX = 0x4A82C2B1;

        public const int NVAPI_OK = 0;
        public const int NVAPI_ERROR = -1;
        public const int NVAPI_NVIDIA_DEVICE_NOT_FOUND = -6;
        public const int NVAPI_END_ENUMERATION = -7;

        // NV_SCALING（官方 nvapi.h）：Balanced = 显示器缩放，Force GPU = GPU 缩放。
        // 地面真值：NVIDIA 控制面板「全屏 + 显示缩放」读回 1，
        // 故显示缩放两项为 1（全屏）/ 6（纵横比），本工具固定使用显示缩放。
        public const int SCALING_DISPLAY_FULLSCREEN = 1; // NV_SCALING_GPU_SCALING_TO_CLOSEST（Balanced - Full Screen）
        public const int SCALING_GPU_FULLSCREEN = 2;     // NV_SCALING_GPU_SCALING_TO_NATIVE（Force GPU - Full Screen）
        public const int SCALING_GPU_CENTERED = 3;       // NV_SCALING_GPU_SCANOUT_TO_NATIVE（Force GPU - Centered）
        public const int SCALING_GPU_ASPECT = 5;         // NV_SCALING_GPU_SCALING_TO_ASPECT_SCANOUT_TO_NATIVE（Force GPU - Aspect Ratio）
        public const int SCALING_DISPLAY_ASPECT = 6;     // NV_SCALING_GPU_SCALING_TO_ASPECT_SCANOUT_TO_CLOSEST（Balanced - Aspect Ratio）
        public const int SCALING_DISPLAY_CENTERED = 7;   // NV_SCALING_GPU_SCANOUT_TO_CLOSEST（Balanced - Centered）
        public const int SCALING_INTEGER = 8;            // NV_SCALING_GPU_INTEGER_ASPECT_SCALING（Force GPU - Integer Scaling）

        private const int MAX_PATHS = 32;   // NVAPI_MAX_DISPLAYCONFIG_PATHS
        private const int MAX_TARGETS = 24; // NVAPI_MAX_DISPLAYCONFIG_TARGETS
        private const int DETAILS_SIZE = 128; // NV_DISPLAYCONFIG_PATH_ADVANCED_TARGET_INFO_V1
        private const uint FLAGS_SAVE_TO_PERSISTENCE = 0x00000002;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr QueryInterfaceDelegate(uint id);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int InitializeDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int UnloadDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GetErrorMessageDelegate(int status, StringBuilder msg);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int EnumNvidiaDisplayHandleDelegate(uint thisEnum, ref IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GetAssociatedNvidiaDisplayNameDelegate(IntPtr handle, StringBuilder name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GetAssociatedDisplayOutputIdDelegate(IntPtr handle, out uint outputId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GetDisplayIdByDisplayNameDelegate([MarshalAs(UnmanagedType.LPStr)] string displayName, out uint displayId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GetDVCInfoExDelegate(IntPtr handle, uint outputId, IntPtr info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SetDVCLevelExDelegate(IntPtr handle, uint outputId, IntPtr info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GetDisplayConfigDelegate(ref uint pathInfoCount, IntPtr pathInfo);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SetDisplayConfigDelegate(uint pathInfoCount, IntPtr pathInfo, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        public static bool Available;
        public static bool DebugLog;
        public static string UnavailableReason = "";

        private static void Log(string msg)
        {
            if (DebugLog) Console.WriteLine("[nvapi] " + msg);
        }
        public static readonly List<string> ResolvedFunctions = new List<string>();
        public static readonly List<string> MissingFunctions = new List<string>();

        private static bool attempted;
        private static UnloadDelegate unload;
        private static GetErrorMessageDelegate getErrorMessage;
        private static EnumNvidiaDisplayHandleDelegate enumDisplayHandle;
        private static GetAssociatedNvidiaDisplayNameDelegate getDisplayName;
        private static GetAssociatedDisplayOutputIdDelegate getOutputId;
        private static GetDisplayIdByDisplayNameDelegate getDisplayIdByName;
        private static GetDVCInfoExDelegate getDvcInfoEx;
        private static SetDVCLevelExDelegate setDvcLevelEx;
        private static GetDisplayConfigDelegate getDisplayConfig;
        private static SetDisplayConfigDelegate setDisplayConfig;

        public static bool Load()
        {
            if (attempted) return Available;
            attempted = true;

            IntPtr lib = LoadLibrary("nvapi64.dll");
            if (lib == IntPtr.Zero) lib = LoadLibrary("nvapi.dll");
            if (lib == IntPtr.Zero)
            {
                UnavailableReason = "nvapi64.dll 未找到（无 NVIDIA 显卡或驱动未安装）";
                return false;
            }

            IntPtr qi = GetProcAddress(lib, "nvapi_QueryInterface");
            if (qi == IntPtr.Zero)
            {
                UnavailableReason = "nvapi_QueryInterface 导出未找到";
                return false;
            }
            QueryInterfaceDelegate query =
                (QueryInterfaceDelegate)Marshal.GetDelegateForFunctionPointer(qi, typeof(QueryInterfaceDelegate));

            InitializeDelegate initialize =
                (InitializeDelegate)Resolve(query, ID_INITIALIZE, "NvAPI_Initialize", typeof(InitializeDelegate));
            unload = (UnloadDelegate)Resolve(query, ID_UNLOAD, "NvAPI_Unload", typeof(UnloadDelegate));
            getErrorMessage = (GetErrorMessageDelegate)Resolve(query, ID_GET_ERROR_MESSAGE, "NvAPI_GetErrorMessage", typeof(GetErrorMessageDelegate));
            enumDisplayHandle = (EnumNvidiaDisplayHandleDelegate)Resolve(query, ID_ENUM_DISPLAY_HANDLE, "NvAPI_EnumNvidiaDisplayHandle", typeof(EnumNvidiaDisplayHandleDelegate));
            getDisplayName = (GetAssociatedNvidiaDisplayNameDelegate)Resolve(query, ID_GET_DISPLAY_NAME, "NvAPI_GetAssociatedNvidiaDisplayName", typeof(GetAssociatedNvidiaDisplayNameDelegate));
            getOutputId = (GetAssociatedDisplayOutputIdDelegate)Resolve(query, ID_GET_OUTPUT_ID, "NvAPI_GetAssociatedDisplayOutputId", typeof(GetAssociatedDisplayOutputIdDelegate));
            getDisplayIdByName = (GetDisplayIdByDisplayNameDelegate)Resolve(query, ID_GET_DISPLAY_ID_BY_NAME, "NvAPI_DISP_GetDisplayIdByDisplayName", typeof(GetDisplayIdByDisplayNameDelegate));
            getDvcInfoEx = (GetDVCInfoExDelegate)Resolve(query, ID_GET_DVC_INFO_EX, "NvAPI_GetDVCInfoEx", typeof(GetDVCInfoExDelegate));
            setDvcLevelEx = (SetDVCLevelExDelegate)Resolve(query, ID_SET_DVC_LEVEL_EX, "NvAPI_SetDVCLevelEx", typeof(SetDVCLevelExDelegate));
            getDisplayConfig = (GetDisplayConfigDelegate)Resolve(query, ID_GET_DISPLAY_CONFIG, "NvAPI_DISP_GetDisplayConfig", typeof(GetDisplayConfigDelegate));
            setDisplayConfig = (SetDisplayConfigDelegate)Resolve(query, ID_SET_DISPLAY_CONFIG, "NvAPI_DISP_SetDisplayConfig", typeof(SetDisplayConfigDelegate));

            if (initialize == null || enumDisplayHandle == null || getDisplayIdByName == null
                || getDisplayConfig == null || setDisplayConfig == null)
            {
                UnavailableReason = "关键函数解析失败：" + string.Join(", ", MissingFunctions.ToArray());
                return false;
            }

            int r = initialize();
            if (r != NVAPI_OK)
            {
                UnavailableReason = "NvAPI_Initialize 返回 " + r + "（" + ErrorString(r) + "）";
                return false;
            }
            Available = true;
            return true;
        }

        private static Delegate Resolve(QueryInterfaceDelegate query, uint id, string name, Type delegateType)
        {
            IntPtr ptr = query(id);
            if (ptr == IntPtr.Zero)
            {
                MissingFunctions.Add(name);
                return null;
            }
            ResolvedFunctions.Add(name);
            return Marshal.GetDelegateForFunctionPointer(ptr, delegateType);
        }

        public static void Unload()
        {
            if (Available && unload != null)
            {
                try { unload(); } catch { }
            }
        }

        public static string ErrorString(int code)
        {
            if (getErrorMessage == null) return "NvAPI 错误 " + code;
            try
            {
                StringBuilder sb = new StringBuilder(64);
                if (getErrorMessage(code, sb) == NVAPI_OK) return sb.ToString();
            }
            catch { }
            return "NvAPI 错误 " + code;
        }

        // ---------- 显示器句柄 / 数字振动 ----------

        private static int FindHandle(string displayName, out IntPtr handle)
        {
            handle = IntPtr.Zero;
            if (enumDisplayHandle == null || getDisplayName == null)
                return -1;
            uint i = 0;
            while (true)
            {
                IntPtr h = IntPtr.Zero;
                int r = enumDisplayHandle(i, ref h);
                if (r == NVAPI_END_ENUMERATION) break;
                if (r != NVAPI_OK) return r;
                i++;
                StringBuilder sb = new StringBuilder(64);
                if (getDisplayName(h, sb) == NVAPI_OK
                    && string.Equals(sb.ToString(), displayName, StringComparison.OrdinalIgnoreCase))
                {
                    handle = h;
                    return NVAPI_OK;
                }
            }
            return NVAPI_NVIDIA_DEVICE_NOT_FOUND;
        }

        // NV_DISPLAY_DVC_INFO_EX：version/currentLevel/minLevel/maxLevel/defaultLevel（5×NvU32=20 字节）
        public static int GetVibrance(string displayName, out int current, out int min, out int max, out int def)
        {
            current = 0; min = 0; max = 100; def = 50;
            if (getDvcInfoEx == null) return -1;
            IntPtr handle;
            int r = FindHandle(displayName, out handle);
            if (r != NVAPI_OK) return r;

            uint outputId;
            if (getOutputId == null || getOutputId(handle, out outputId) != NVAPI_OK) outputId = 0;

            IntPtr buf = Marshal.AllocHGlobal(20);
            try
            {
                ZeroFill(buf, 20);
                Marshal.WriteInt32(buf, 0, 20 | (1 << 16)); // MAKE_NVAPI_VERSION(…,1)
                r = getDvcInfoEx(handle, outputId, buf);
                if (r != NVAPI_OK) return r;
                current = Marshal.ReadInt32(buf, 4);
                min = Marshal.ReadInt32(buf, 8);
                max = Marshal.ReadInt32(buf, 12);
                def = Marshal.ReadInt32(buf, 16);
                return NVAPI_OK;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        public static int SetVibrance(string displayName, int level)
        {
            try
            {
                if (setDvcLevelEx == null) return -1;
                IntPtr handle;
                int r = FindHandle(displayName, out handle);
                if (r != NVAPI_OK) return r;

                uint outputId;
                if (getOutputId == null || getOutputId(handle, out outputId) != NVAPI_OK) outputId = 0;

                IntPtr buf = Marshal.AllocHGlobal(20);
                try
                {
                    ZeroFill(buf, 20);
                    Marshal.WriteInt32(buf, 0, 20 | (1 << 16));
                    r = getDvcInfoEx(handle, outputId, buf); // 先读出 min/max/default
                    if (r != NVAPI_OK) return r;
                    int min = Marshal.ReadInt32(buf, 8);
                    int max = Marshal.ReadInt32(buf, 12);
                    if (level < min) level = min;
                    if (level > max) level = max;
                    Marshal.WriteInt32(buf, 4, level);
                    return setDvcLevelEx(handle, outputId, buf);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch (Exception ex)
            {
                Log("SetVibrance 异常: " + ex.GetType().Name + ": " + ex.Message);
                return NVAPI_ERROR;
            }
        }

        // ---------- 缩放模式（NvAPI_DISP_Get/SetDisplayConfig） ----------

        public static int GetScaling(string displayName, out int scaling)
        {
            scaling = -1;
            if (getDisplayIdByName == null || getDisplayConfig == null) return -1;
            uint displayId;
            int r = getDisplayIdByName(displayName, out displayId);
            if (r != NVAPI_OK) return r;

            using (DisplayConfigBuffer cfg = new DisplayConfigBuffer())
            {
                r = cfg.Get(getDisplayConfig);
                if (r != NVAPI_OK) return r;
                IntPtr details;
                if (!cfg.FindTargetDetails(displayId, out details)) return NVAPI_NVIDIA_DEVICE_NOT_FOUND;
                scaling = Marshal.ReadInt32(details, 8); // version(0) rotation(4) scaling(8)
                return NVAPI_OK;
            }
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        public static int SetScaling(string displayName, int scaling)
        {
            try
            {
                if (getDisplayIdByName == null || getDisplayConfig == null || setDisplayConfig == null) return -1;
                uint displayId;
                int r = getDisplayIdByName(displayName, out displayId);
                Log("GetDisplayIdByDisplayName -> " + r + ", displayId=0x" + displayId.ToString("X"));
                if (r != NVAPI_OK) return r;

                using (DisplayConfigBuffer cfg = new DisplayConfigBuffer())
                {
                    r = cfg.Get(getDisplayConfig);
                    Log("GetDisplayConfig -> " + r + ", paths=" + cfg.Count);
                    if (r != NVAPI_OK) return r;
                    cfg.Dump();
                    IntPtr details;
                    if (!cfg.FindTargetDetails(displayId, out details))
                    {
                        Log("FindTargetDetails 未找到 displayId");
                        return NVAPI_NVIDIA_DEVICE_NOT_FOUND;
                    }
                    int oldScaling = Marshal.ReadInt32(details, 8);
                    Log("details=0x" + details.ToString("X") + " oldScaling=" + oldScaling + " -> new=" + scaling);
                    Marshal.WriteInt32(details, 8, scaling); // 只改目标 path 的 scaling 字段
                    Log("调用 SetDisplayConfig(count=" + cfg.Count + ", flags=SAVE_TO_PERSISTENCE)...");
                    r = setDisplayConfig(cfg.Count, cfg.Paths, FLAGS_SAVE_TO_PERSISTENCE);
                    Log("SetDisplayConfig -> " + r);
                    if (r != NVAPI_OK) return r;
                }

                // 重新读取验证
                int verify;
                r = GetScaling(displayName, out verify);
                Log("验证回读 GetScaling -> " + r + ", scaling=" + verify);
                if (r != NVAPI_OK) return r;
                return verify == scaling ? NVAPI_OK : NVAPI_ERROR; // 验证不一致
            }
            catch (Exception ex)
            {
                Log("异常: " + ex.GetType().Name + ": " + ex.Message);
                return NVAPI_ERROR;
            }
        }

        // NV_DISPLAYCONFIG_PATH_INFO_V2 指针式结构的原始缓冲区。
        // x64: path=48, target=24；x86: path=28, target=12；details 固定 128。
        private sealed class DisplayConfigBuffer : IDisposable
        {
            public IntPtr Paths;
            public IntPtr Targets;
            public IntPtr Details;
            public IntPtr SourceModes;
            public uint Count;

            private const int SOURCE_MODE_SIZE = 32; // NV_DISPLAYCONFIG_SOURCE_MODE_INFO_V1

            private readonly int pathSize;
            private readonly int targetSize;
            private readonly int offPathTargetCount;
            private readonly int offPathTargetPtr;
            private readonly int offPathSourceModePtr;
            private readonly int offTargetDetails;

            public DisplayConfigBuffer()
            {
                bool x64 = IntPtr.Size == 8;
                pathSize = x64 ? 48 : 28;
                targetSize = x64 ? 24 : 12;
                offPathTargetCount = 8;
                offPathTargetPtr = x64 ? 16 : 12;
                offPathSourceModePtr = x64 ? 24 : 16;
                offTargetDetails = x64 ? 8 : 4;

                Paths = Marshal.AllocHGlobal(MAX_PATHS * pathSize);
                Targets = Marshal.AllocHGlobal(MAX_PATHS * MAX_TARGETS * targetSize);
                Details = Marshal.AllocHGlobal(MAX_PATHS * MAX_TARGETS * DETAILS_SIZE);
                SourceModes = Marshal.AllocHGlobal(MAX_PATHS * SOURCE_MODE_SIZE);
                ZeroFill(Paths, MAX_PATHS * pathSize);
                ZeroFill(Targets, MAX_PATHS * MAX_TARGETS * targetSize);
                ZeroFill(Details, MAX_PATHS * MAX_TARGETS * DETAILS_SIZE);
                ZeroFill(SourceModes, MAX_PATHS * SOURCE_MODE_SIZE);

                for (int i = 0; i < MAX_PATHS; i++)
                {
                    IntPtr path = PathAt(i);
                    Marshal.WriteInt32(path, 0, unchecked((int)((uint)pathSize | (2u << 16)))); // PATH_INFO_VER2
                    Marshal.WriteInt32(path, offPathTargetCount, MAX_TARGETS);
                    Marshal.WriteIntPtr(path, offPathTargetPtr, TargetRowAt(i));
                    Marshal.WriteIntPtr(path, offPathSourceModePtr, SourceModeAt(i));
                    for (int j = 0; j < MAX_TARGETS; j++)
                    {
                        IntPtr det = DetailsAt(i, j);
                        Marshal.WriteIntPtr(TargetAt(i, j), offTargetDetails, det);
                        Marshal.WriteInt32(det, 0, DETAILS_SIZE | (1 << 16)); // ADVANCED_TARGET_INFO_VER1
                    }
                }
            }

            public int Get(GetDisplayConfigDelegate getter)
            {
                // 官方文档三遍调用模式：第一遍 pathInfo=NULL 取实际路径数
                Count = 0;
                int r = getter(ref Count, IntPtr.Zero);
                Log("第一遍 GetDisplayConfig(NULL) -> " + r + ", 实际路径数=" + Count);
                if (r != NVAPI_OK) return r;
                if (Count == 0 || Count > MAX_PATHS) return NVAPI_ERROR;
                uint capacity = Count;
                r = getter(ref Count, Paths);
                Log("第二遍 GetDisplayConfig -> " + r + ", 填充路径数=" + Count + "（容量 " + capacity + "）");
                return r;
            }

            public void Dump()
            {
                for (int i = 0; i < Count; i++)
                {
                    IntPtr path = PathAt(i);
                    int tc = Marshal.ReadInt32(path, offPathTargetCount);
                    uint srcId = (uint)Marshal.ReadInt32(path, 4);
                    int nonNv = Marshal.ReadInt32(path, x64() ? 32 : 20) & 1;
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    sb.Append("path[").Append(i).Append("] srcId=").Append(srcId)
                      .Append(" targets=").Append(tc).Append(" nonNV=").Append(nonNv);
                    if (tc < 0 || tc > MAX_TARGETS) tc = 0;
                    for (int j = 0; j < tc && j < 4; j++)
                    {
                        IntPtr target = TargetAt(i, j);
                        uint did = (uint)Marshal.ReadInt32(target, 0);
                        IntPtr det = Marshal.ReadIntPtr(target, offTargetDetails);
                        int sc = det != IntPtr.Zero ? Marshal.ReadInt32(det, 8) : -99;
                        sb.Append(" | t").Append(j).Append(": id=0x").Append(did.ToString("X"))
                          .Append(" scaling=").Append(sc);
                    }
                    Log(sb.ToString());
                }
            }

            private static bool x64()
            {
                return IntPtr.Size == 8;
            }

            public bool FindTargetDetails(uint displayId, out IntPtr details)
            {
                details = IntPtr.Zero;
                for (int i = 0; i < Count; i++)
                {
                    IntPtr path = PathAt(i);
                    int targetCount = Marshal.ReadInt32(path, offPathTargetCount);
                    if (targetCount < 0 || targetCount > MAX_TARGETS) continue;
                    for (int j = 0; j < targetCount; j++)
                    {
                        IntPtr target = TargetAt(i, j);
                        if ((uint)Marshal.ReadInt32(target, 0) != displayId) continue;
                        details = Marshal.ReadIntPtr(target, offTargetDetails);
                        return details != IntPtr.Zero;
                    }
                }
                return false;
            }

            private IntPtr PathAt(int i)
            {
                return new IntPtr(Paths.ToInt64() + (long)i * pathSize);
            }

            private IntPtr TargetRowAt(int i)
            {
                return new IntPtr(Targets.ToInt64() + (long)i * MAX_TARGETS * targetSize);
            }

            private IntPtr TargetAt(int i, int j)
            {
                return new IntPtr(Targets.ToInt64() + ((long)i * MAX_TARGETS + j) * targetSize);
            }

            private IntPtr DetailsAt(int i, int j)
            {
                return new IntPtr(Details.ToInt64() + ((long)i * MAX_TARGETS + j) * DETAILS_SIZE);
            }

            private IntPtr SourceModeAt(int i)
            {
                return new IntPtr(SourceModes.ToInt64() + (long)i * SOURCE_MODE_SIZE);
            }

            public void Dispose()
            {
                if (Paths != IntPtr.Zero) Marshal.FreeHGlobal(Paths);
                if (Targets != IntPtr.Zero) Marshal.FreeHGlobal(Targets);
                if (Details != IntPtr.Zero) Marshal.FreeHGlobal(Details);
                if (SourceModes != IntPtr.Zero) Marshal.FreeHGlobal(SourceModes);
                Paths = Targets = Details = SourceModes = IntPtr.Zero;
            }
        }

        private static void ZeroFill(IntPtr ptr, int size)
        {
            long end = size & ~7L;
            for (long i = 0; i < end; i += 8)
                Marshal.WriteInt64(ptr, (int)i, 0);
            for (long i = end; i < size; i++)
                Marshal.WriteByte(ptr, (int)i, 0);
        }
    }
}
