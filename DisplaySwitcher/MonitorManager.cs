using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DisplaySwitcher
{
    internal class MonitorDevice
    {
        public string InstanceId;    // 设备实例 ID，形如 MONITOR\AOC2702\4&xxxx
        public string FriendlyName;
        public bool IsEnabled;
        public bool StatusKnown = true; // CM_Get_DevNode_Status 读取失败时为 false

        public override string ToString()
        {
            if (!StatusKnown) return FriendlyName + "（状态未知）";
            return FriendlyName + (IsEnabled ? "（已启用）" : "（已禁用）");
        }
    }

    // 监视器设备（PNP）枚举与禁用/启用。已禁用的监视器不会出现
    // 在 EnumDisplayDevices 里，所以数据源用 SetupDi 枚举（含禁用项）。
    // DIGCF_PRESENT 只列当前实际存在的设备，过滤历史上插过又拔掉的幽灵监视器。
    internal static class MonitorManager
    {
        public static List<MonitorDevice> GetMonitors()
        {
            List<MonitorDevice> result = new List<MonitorDevice>();
            Guid classGuid = NativeMethods.GUID_DEVCLASS_MONITOR;
            IntPtr set = NativeMethods.SetupDiGetClassDevs(ref classGuid, IntPtr.Zero, IntPtr.Zero, NativeMethods.DIGCF_PRESENT);
            if (set == IntPtr.Zero || set == new IntPtr(-1)) return result;
            try
            {
                uint index = 0;
                while (true)
                {
                    NativeMethods.SP_DEVINFO_DATA data = new NativeMethods.SP_DEVINFO_DATA();
                    data.cbSize = Marshal.SizeOf(typeof(NativeMethods.SP_DEVINFO_DATA));
                    if (!NativeMethods.SetupDiEnumDeviceInfo(set, index, ref data)) break;
                    index++;

                    MonitorDevice dev = new MonitorDevice();
                    StringBuilder id = new StringBuilder(256);
                    int required;
                    if (NativeMethods.SetupDiGetDeviceInstanceId(set, ref data, id, id.Capacity, out required))
                        dev.InstanceId = id.ToString();
                    else
                        continue;

                    dev.FriendlyName = GetStringProperty(set, ref data, NativeMethods.SPDRP_FRIENDLYNAME);
                    if (string.IsNullOrEmpty(dev.FriendlyName))
                        dev.FriendlyName = GetStringProperty(set, ref data, NativeMethods.SPDRP_DEVICEDESC);
                    if (string.IsNullOrEmpty(dev.FriendlyName))
                        dev.FriendlyName = dev.InstanceId;

                    int status, problem;
                    if (NativeMethods.CM_Get_DevNode_Status(out status, out problem, data.DevInst, 0) == NativeMethods.CR_SUCCESS)
                        dev.IsEnabled = !((status & NativeMethods.DN_HAS_PROBLEM) != 0 && problem == NativeMethods.CM_PROB_DISABLED);
                    else
                    {
                        // 状态读不出来时标未知，不瞎报已启用
                        dev.StatusKnown = false;
                        dev.IsEnabled = true; // 计数时按启用算，避免误删最后一台
                    }

                    result.Add(dev);
                }
            }
            finally
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(set);
            }
            return result;
        }

        private static string GetStringProperty(IntPtr set, ref NativeMethods.SP_DEVINFO_DATA data, uint property)
        {
            StringBuilder sb = new StringBuilder(256);
            uint regType;
            uint required;
            if (NativeMethods.SetupDiGetDeviceRegistryProperty(set, ref data, property, out regType, sb, (uint)sb.Capacity, out required))
                return sb.ToString();
            return null;
        }

        // 返回 CONFIGRET，CR_SUCCESS(0) 表示成功；CR_ACCESS_DENIED(0x33) 表示需要管理员
        public static int Disable(string instanceId)
        {
            uint devInst;
            int cr = NativeMethods.CM_Locate_DevNode(out devInst, instanceId, NativeMethods.CM_LOCATE_DEVNODE_NORMAL);
            if (cr != NativeMethods.CR_SUCCESS) return cr;
            return NativeMethods.CM_Disable_DevNode(devInst, NativeMethods.CM_DISABLE_UI_NOT_OK | NativeMethods.CM_DISABLE_PERSIST);
        }

        public static int Enable(string instanceId)
        {
            uint devInst;
            int cr = NativeMethods.CM_Locate_DevNode(out devInst, instanceId, NativeMethods.CM_LOCATE_DEVNODE_NORMAL);
            if (cr != NativeMethods.CR_SUCCESS) return cr;
            return NativeMethods.CM_Enable_DevNode(devInst, 0);
        }

        public static int CountEnabled(List<MonitorDevice> monitors)
        {
            int n = 0;
            for (int i = 0; i < monitors.Count; i++)
                if (monitors[i].IsEnabled) n++;
            return n;
        }

        public static MonitorDevice FindByInstanceId(List<MonitorDevice> monitors, string instanceId)
        {
            for (int i = 0; i < monitors.Count; i++)
                if (string.Equals(monitors[i].InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                    return monitors[i];
            return null;
        }

        public static string ErrorText(int cr)
        {
            if (cr == NativeMethods.CR_SUCCESS) return "成功";
            if (cr == NativeMethods.CR_ACCESS_DENIED) return "权限不足，需要管理员权限";
            return "CONFIGRET=0x" + cr.ToString("X2");
        }
    }
}
