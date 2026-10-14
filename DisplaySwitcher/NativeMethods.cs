using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DisplaySwitcher
{
    internal static class NativeMethods
    {
        // DEVMODE.dmFields 标志
        public const int DM_PELSWIDTH = 0x00080000;
        public const int DM_PELSHEIGHT = 0x00100000;
        public const int DM_DISPLAYFREQUENCY = 0x00400000;

        // ChangeDisplaySettingsEx 标志
        public const int CDS_UPDATEREGISTRY = 0x00000001;
        public const int CDS_TEST = 0x00000002;
        public const int CDS_FULLSCREEN = 0x00000004;

        // ChangeDisplaySettingsEx 返回值
        public const int DISP_CHANGE_SUCCESSFUL = 0;
        public const int DISP_CHANGE_RESTART = 1;
        public const int DISP_CHANGE_FAILED = -1;
        public const int DISP_CHANGE_BADMODE = -2;
        public const int DISP_CHANGE_NOTUPDATED = -3;
        public const int DISP_CHANGE_BADFLAGS = -4;
        public const int DISP_CHANGE_BADPARAM = -5;

        // DISPLAY_DEVICE.StateFlags
        public const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;

        // EnumDisplaySettings 的 modeNum 特殊值
        public const int ENUM_CURRENT_SETTINGS = -1;

        public const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        // ---- cfgmgr32（常量已对照官方 cfgmgr32.h 核对）----
        public const int CR_SUCCESS = 0x00000000;
        public const int CR_ACCESS_DENIED = 0x00000033;
        public const int CM_LOCATE_DEVNODE_NORMAL = 0x00000000;
        public const int CM_DISABLE_UI_NOT_OK = 0x00000004;
        public const int CM_DISABLE_PERSIST = 0x00000008; // 跨重启保持禁用
        public const int CM_PROB_DISABLED = 22;           // Code 22：设备已禁用
        public const int DN_HAS_PROBLEM = 0x00000400;

        // ---- setupapi ----
        public const uint DIGCF_PRESENT = 0x00000002;
        public const uint SPDRP_DEVICEDESC = 0x00000000;
        public const uint SPDRP_FRIENDLYNAME = 0x0000000C;

        // GUID_DEVCLASS_MONITOR = {4d36e96e-e325-11ce-bfc1-08002be10318}
        public static readonly Guid GUID_DEVCLASS_MONITOR =
            new Guid(0x4d36e96e, 0xe325, 0x11ce, 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18);

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInstanceId(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData,
            StringBuilder DeviceInstanceId, int DeviceInstanceIdSize, out int RequiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiGetDeviceRegistryProperty(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData,
            uint Property, out uint PropertyRegDataType, StringBuilder PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Auto)]
        public static extern int CM_Locate_DevNode(out uint pdnDevInst, string pDeviceID, int ulFlags);

        [DllImport("cfgmgr32.dll")]
        public static extern int CM_Disable_DevNode(uint dnDevInst, int ulFlags);

        [DllImport("cfgmgr32.dll")]
        public static extern int CM_Enable_DevNode(uint dnDevInst, int ulFlags);

        [DllImport("cfgmgr32.dll")]
        public static extern int CM_Get_DevNode_Status(out int pulStatus, out int pulProblemNumber, uint dnDevInst, int ulFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, int dwflags, IntPtr lParam);

        // winexe 程序附加到父控制台，用于 --list 等命令行输出
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FreeConsole();
    }

    internal class DisplayInfo
    {
        public string DeviceName;      // \\.\DISPLAY1
        public string AdapterString;   // 显卡名称
        public string MonitorName;     // 显示器友好名
        public string MonitorInstanceId; // 监视器设备实例 ID（形如 MONITOR\AOC2702\4&xxxx）
        public int CurrentWidth;
        public int CurrentHeight;
        public int CurrentFrequency;

        public override string ToString()
        {
            string friendly = string.IsNullOrEmpty(MonitorName) ? AdapterString : MonitorName;
            return DeviceName + " (" + friendly + ")";
        }
    }

    internal static class DisplayManager
    {
        public static List<DisplayInfo> GetDisplays()
        {
            List<DisplayInfo> result = new List<DisplayInfo>();
            uint i = 0;
            while (true)
            {
                NativeMethods.DISPLAY_DEVICE dd = new NativeMethods.DISPLAY_DEVICE();
                dd.cb = Marshal.SizeOf(typeof(NativeMethods.DISPLAY_DEVICE));
                if (!NativeMethods.EnumDisplayDevices(null, i, ref dd, 0)) break;
                i++;
                if ((dd.StateFlags & NativeMethods.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0) continue;

                DisplayInfo info = new DisplayInfo();
                info.DeviceName = dd.DeviceName;
                info.AdapterString = dd.DeviceString;

                NativeMethods.DISPLAY_DEVICE mon = new NativeMethods.DISPLAY_DEVICE();
                mon.cb = Marshal.SizeOf(typeof(NativeMethods.DISPLAY_DEVICE));
                if (NativeMethods.EnumDisplayDevices(dd.DeviceName, 0, ref mon, 0))
                {
                    info.MonitorName = mon.DeviceString;
                    info.MonitorInstanceId = mon.DeviceID;
                }

                NativeMethods.DEVMODE cur = new NativeMethods.DEVMODE();
                cur.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));
                if (NativeMethods.EnumDisplaySettings(dd.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref cur))
                {
                    info.CurrentWidth = cur.dmPelsWidth;
                    info.CurrentHeight = cur.dmPelsHeight;
                    info.CurrentFrequency = cur.dmDisplayFrequency;
                }
                result.Add(info);
            }
            return result;
        }

        public static List<NativeMethods.DEVMODE> GetModes(string deviceName)
        {
            List<NativeMethods.DEVMODE> modes = new List<NativeMethods.DEVMODE>();
            int i = 0;
            while (true)
            {
                NativeMethods.DEVMODE dm = new NativeMethods.DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));
                if (!NativeMethods.EnumDisplaySettings(deviceName, i, ref dm)) break;
                modes.Add(dm);
                i++;
            }
            return modes;
        }

        public static NativeMethods.DEVMODE GetCurrentMode(string deviceName)
        {
            NativeMethods.DEVMODE dm = new NativeMethods.DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));
            NativeMethods.EnumDisplaySettings(deviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref dm);
            return dm;
        }

        public static bool DeviceExists(string deviceName)
        {
            List<DisplayInfo> list = GetDisplays();
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i].DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public static string FindDeviceByFriendlyName(string friendlyName)
        {
            if (string.IsNullOrEmpty(friendlyName)) return null;
            List<DisplayInfo> list = GetDisplays();
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].MonitorName, friendlyName, StringComparison.OrdinalIgnoreCase))
                    return list[i].DeviceName;
                if (string.Equals(list[i].AdapterString, friendlyName, StringComparison.OrdinalIgnoreCase))
                    return list[i].DeviceName;
            }
            return null;
        }

        // 仅测试模式是否被驱动接受（不应用）
        public static int TestMode(string deviceName, int width, int height, int frequency)
        {
            NativeMethods.DEVMODE dm = new NativeMethods.DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));
            dm.dmPelsWidth = width;
            dm.dmPelsHeight = height;
            dm.dmDisplayFrequency = frequency;
            dm.dmFields = NativeMethods.DM_PELSWIDTH | NativeMethods.DM_PELSHEIGHT | NativeMethods.DM_DISPLAYFREQUENCY;
            return NativeMethods.ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero,
                NativeMethods.CDS_FULLSCREEN | NativeMethods.CDS_TEST, IntPtr.Zero);
        }

        // 先用 CDS_TEST 验证模式合法，再用 CDS_UPDATEREGISTRY 真正应用
        public static int SetMode(string deviceName, int width, int height, int frequency)
        {
            NativeMethods.DEVMODE dm = new NativeMethods.DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));
            dm.dmPelsWidth = width;
            dm.dmPelsHeight = height;
            dm.dmDisplayFrequency = frequency;
            dm.dmFields = NativeMethods.DM_PELSWIDTH | NativeMethods.DM_PELSHEIGHT | NativeMethods.DM_DISPLAYFREQUENCY;

            int test = NativeMethods.ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero,
                NativeMethods.CDS_FULLSCREEN | NativeMethods.CDS_TEST, IntPtr.Zero);
            if (test != NativeMethods.DISP_CHANGE_SUCCESSFUL) return test;

            return NativeMethods.ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero,
                NativeMethods.CDS_UPDATEREGISTRY, IntPtr.Zero);
        }

        public static string ErrorText(int code)
        {
            switch (code)
            {
                case NativeMethods.DISP_CHANGE_SUCCESSFUL: return "成功";
                case NativeMethods.DISP_CHANGE_RESTART: return "需要重启才能生效";
                case NativeMethods.DISP_CHANGE_FAILED: return "显示驱动执行切换失败";
                case NativeMethods.DISP_CHANGE_BADMODE: return "不支持该显示模式";
                case NativeMethods.DISP_CHANGE_NOTUPDATED: return "无法写入注册表设置";
                case NativeMethods.DISP_CHANGE_BADFLAGS: return "传入的标志无效";
                case NativeMethods.DISP_CHANGE_BADPARAM: return "参数无效";
                default: return "未知错误";
            }
        }
    }
}
