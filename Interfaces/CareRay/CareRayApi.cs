using System.Runtime.InteropServices; //调用C++的dll必须
using System;

namespace Interfaces
{
    public struct CrSystemInfo
    {
        public uint raw_img_width;
        public uint raw_img_height;
        public uint frame_header_len;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string hardware_version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string serial_number;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string software_version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string firmware_version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string detr_machine_id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string detr_desc;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class CrEvent
    {
        public int event_id;
        public int detr_index;
        public int width;
        public int height;
        public int pixel_depth;
        public int header_len;
        public IntPtr data_ptr;
    }
    public enum CrEventId
    {
        CR_EVT_DETR_DISCONNECTED       = 1,
        CR_EVT_TEMPERATURE_INFO        = 3,
        CR_EVT_BATTERY_INFO            = 4,
        CR_EVT_WIRELESS_INFO           = 5,
        CR_EVT_NEW_FRAME               = 6,
        CR_EVT_CALIBRATION_IN_PROGRESS = 7,
        CR_EVT_CALIBRATION_FINISHED    = 8,
        CR_EVT_ACQ_STAT_INFO           = 10,
        CR_EVT_RAD_ACQ_IN_PROGRESS     = 11,
        CR_EVT_DETR_RECONNECTED        = 13,
        CR_EVT_DISCARD_FRAME           = 15,
    }

    unsafe class CareRayApi
    {
        public delegate void CrEventCallbackFun(int detr_idx, IntPtr event_info);

        [DllImport("CareRayApi.dll", EntryPoint = "CrInitializeLibrary", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrInitializeLibrary();

        [DllImport("CareRayApi.dll", EntryPoint = "CrDeinitializeLibrary", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrDeinitializeLibrary();

        [DllImport("CareRayApi.dll", EntryPoint = "CrGetLastIntlMsg", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static void CrGetLastIntlMsg(ref byte message_buffer, int buffer_length);
        [DllImport("CareRayApi.dll", EntryPoint = "CrRegisterEventCallbackFun", CallingConvention = CallingConvention.StdCall)]
        internal extern static int CrRegisterEventCallbackFun(CrEventCallbackFun callbak_ptr);
        [DllImport("CareRayApi.dll", EntryPoint = "CrConnect", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrConnect(int detr_index);

        [DllImport("CareRayApi.dll", EntryPoint = "CrResetDetector", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrResetDetector(int detr_index, int need_reboot);

        [DllImport("CareRayApi.dll", EntryPoint = "CrDisconnect", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrDisconnect(int detr_index);

        [DllImport("CareRayApi.dll", EntryPoint = "CrGetSystemInformation", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrGetSystemInformation(int detr_index, ref CrSystemInfo sys_info);

        [DllImport("CareRayApi.dll", EntryPoint = "CrStartAcquisition", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrStartAcquisition(int detr_index, int app_mode_key, int transfer_iamges_by_callback);

        [DllImport("CareRayApi.dll", EntryPoint = "CrStopAcquisition", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrStopAcquisition(int detr_index);

        [DllImport("CareRayApi.dll", EntryPoint = "CrStartDarkCalibration", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrStartDarkCalibration(int detr_index, int app_mode_key, int is_traditional_type, int is_update_defect);

        [DllImport("CareRayApi.dll", EntryPoint = "CrStartGainCalibration", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrStartGainCalibration(int detr_index, int app_mode_key);

        [DllImport("CareRayApi.dll", EntryPoint = "CrStopCalibration", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrStopCalibration(int detr_index);

        [DllImport("CareRayApi.dll", EntryPoint = "CrPermitExposure", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrPermitExposure(int detr_index);

        [DllImport("CareRayApi.dll", EntryPoint = "CrRequestExposure", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrRequestExposure(int detr_index);

        [DllImport("CareRayApi.dll", EntryPoint = "CrGetImage", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrGetImage(int detr_index, ref byte image_buffer, int buffer_length, int wait_milliseconds);

        [DllImport("CareRayApi.dll", EntryPoint = "CrGetApplicationModeInJson", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static void CrGetApplicationModeInJson(int detr_index, ref byte dest_string_buffer);

        [DllImport("CareRayApi.dll", EntryPoint = "CrGenerateSampleCustomModeConfigFile", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static void CrGenerateSampleCustomModeConfigFile(int detr_index);

        [DllImport("CareRayApi.dll", EntryPoint = "CrRegisterCustomModeFromConfigFile", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static void CrRegisterCustomModeFromConfigFile(int detr_index, ref byte result_string_buffer);

        [DllImport("CareRayApi.dll", EntryPoint = "CrGetAcquisitionProgressInJson", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static void CrGetAcquisitionProgressInJson(int detr_index, ref byte result_string_buffer);

        [DllImport("CareRayApi.dll", EntryPoint = "CrGetCalibrationProgressInJson", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static void CrGetCalibrationProgressInJson(int detr_index, ref byte result_string_buffer);
        [DllImport("CareRayApi.dll", EntryPoint = "CrStartAcquisitionWithCorrOpt", CallingConvention = CallingConvention.StdCall)]
        public unsafe extern static int CrStartAcquisitionWithCorrOpt(int detr_index, int app_mode_key, uint correct_options, int transfer_iamges_by_callback);

    }
}
