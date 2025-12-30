using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using Microsoft.Identity.Client;

namespace Interfaces
{
    public class CrModeInfo
    {
        public int mode_id { get; set; }
        public int image_width { get; set; }
        public int image_height { get; set; }
        public int cutoff_x { get; set; }
        public int cutoff_y { get; set; }
        public float max_frame_rate { get; set; }
        public float max_exposure_time { get; set; }
        public List<int> trigger_types { get; set; }
        public List<int> gain_levels { get; set; }
        public string desc { get; set; }
    }
    public class CrSupportModeList
    {
        public int detr_index { get; set; }
        public int result_code { get; set; }
        public string result_desc { get; set; }
        public List<CrModeInfo> mode_infos { get; set; }
    }
    public class CrRegModeResult
    {
        public int app_mode_key { get; set; }
        public string mode_desc { get; set; }
        public int register_result { get; set; }
        public string register_desc { get; set; }
    }
    public class CrRegModeResultList
    {
        public int detr_index { get; set; }
        public int result_code { get; set; }
        public string result_desc { get; set; }
        public List<CrRegModeResult> register_results { get; set; }
    }
    public class CrAcquisitionProgress
    {
        public int detr_index { get; set; }
        public int result_code { get; set; }
        public string result_desc { get; set; }
        public int exposure_status { get; set; }
        public int is_fetchable { get; set; }
        public int progress_result { get; set; }
    }
    public class CrCalibProgress
    {
        public int detr_index { get; set; }
        public int result_code { get; set; }
        public string result_desc { get; set; }
        public int total_frame_num { get; set; }
        public int current_frame_num { get; set; }
        public int current_frame_mean { get; set; }
        public int target_gray_value { get; set; }
        public int exposure_status { get; set; }
        public int progress_result { get; set; }
        public string progress_error_msg { get; set; }
    }
    public class CrCustomModeInfo
    {
        public int detr_index { get; set; }
        public int app_mode_key { get; set; }
        public int mode_id { get; set; }
        public int image_width { get; set; }
        public int image_height { get; set; }
        public float frame_rate { get; set; }
        public float exposure_time { get; set; }
        public int trigger_type { get; set; }
        public int gain_level { get; set; }
        public int corrections { get; set; }
        public string desc { get; set; }
        public string custom_desc { get; set; }
    }
    public class CrCustomModeInfoList
    {
        public List<CrCustomModeInfo> custom_mode_infos { get; set; }
    }

    unsafe public class CareRayOperator
    {        
        public enum CrRegModeType
        {
            REGED_UNKNOWN_MODE_TYPE = -1,
            REGED_FLUORO_MODE_TYPE = 0,
            REGED_RAD_MODE_TYPE = 1
        }

        public enum CrExpStatus
        {
            CR_EXP_ERROR = -1,
            CR_EXP_INIT,
            CR_EXP_READY,
            CR_EXP_WAIT_PERMISSION,
            CR_EXP_PERMITTED,
            CR_EXP_EXPOSE,
            CR_EXP_COMPLETE,
        }
        public enum CrProcChainOpt
        {
            CR_PROCCHAIN_SANITYCHECK  = 0x01,
            CR_PROCCHAIN_DARKCORR     = 0x02,
            CR_PROCCHAIN_GAINCORR     = 0x04,
            CR_PROCCHAIN_DEFECTCORR   = 0x08,
            CR_PROCCHAIN_LAGCORR      = 0x10,
            CR_PROCCHAIN_IMGCROPPING  = 0x20,
            CR_PROCCHAIN_RTPIXELCORR  = 0x40,
            CR_PROCCHAIN_DENOISING    = 0x80,
            CR_PROCCHAIN_STEPSUPPRESS = 0x100,
            CR_PROCCHAIN_ROTATE_IAMGE = 0x200,
            CR_PROCCHAIN_SHARPEN      = 0x4000
        }

        public const int CR_FLUORO_FRAME_HEADER_LENGTH = 256;
        public const int CR_RAD_FRAME_HEADER_LENGTH = 65536;
        public static CrCustomModeInfoList custom_mode_list;
        public static Dictionary<int, CrSupportModeList> support_mode_list;
        public static Dictionary<int, CrRegModeResultList> reg_result_list;
        public const int CR_MIN_ERROR_CODE = 300000;
        //public const string CR_CUSTOM_MODE_FILE_PATH = "./conf/CustomMode.ini";
        public const string CR_CUSTOM_MODE_FILE_PATH = "conf/CustomMode.ini";
        public static byte[] frame_copy;

        public static void ProcessCallback(int detr_idx, IntPtr event_info)
        {
            CrEvent event_data = (CrEvent)Marshal.PtrToStructure(event_info, typeof(CrEvent));
            //Console.WriteLine("Event from detector {0}, event id is {1}", detr_idx, event_data.event_id);

            switch (event_data.event_id)
            {
                case (int)CrEventId.CR_EVT_NEW_FRAME:
                    {
                        int frame_size = CR_FLUORO_FRAME_HEADER_LENGTH + event_data.width * event_data.height * event_data.pixel_depth / 8;
                        if (frame_copy == null)
                        {
                            frame_copy = new byte[100 * 1024 * 1024]; // 100MB
                        }

                        Marshal.Copy(event_data.data_ptr, frame_copy, 0, frame_size);
                        Console.WriteLine("Fluoro frame number in frame header is: {0}", BitConverter.ToInt32(frame_copy, 8));

                        //// save raw to file
                        //using (FileStream fs = File.Open(BitConverter.ToInt32(frame_copy, 8).ToString() + ".raw", FileMode.Create, FileAccess.Write))
                        //{
                        //    fs.Seek(0, SeekOrigin.End);
                        //    fs.Write(frame_copy, 0, frame_size);
                        //    fs.Close();
                        //    fs.Dispose();
                        //}
                    }
                    break;
                default:
                    break;
            }
        }

        public static string GetLastErrorMsg()
        {
            byte[] recv_cmd_buff = new byte[1024];
            CareRayApi.CrGetLastIntlMsg(ref recv_cmd_buff[0], 1024);
            return System.Text.Encoding.UTF8.GetString(recv_cmd_buff);
        }

        public static int InitLibrary()
        {
            support_mode_list = new Dictionary<int, CrSupportModeList>();
            reg_result_list = new Dictionary<int, CrRegModeResultList>();
            int ret_code = CareRayApi.CrInitializeLibrary();

            CareRayApi.CrEventCallbackFun callback_fun = new CareRayApi.CrEventCallbackFun(ProcessCallback);
            CareRayApi.CrRegisterEventCallbackFun(callback_fun);

            return ret_code;
        }
        public static int DeinitLibrary()
        {
            return CareRayApi.CrDeinitializeLibrary();
        }
        public static int Connect(int detr_index)
        {
            int ret_code = CareRayApi.CrConnect(detr_index);
            if (ret_code < CR_MIN_ERROR_CODE)
            {
                GetSysInfo(detr_index);
            }
            else
            {
                Console.WriteLine("Connect failed: error code = {0}, msg = {1}.", ret_code, GetLastErrorMsg());
            }

            return ret_code;
        }
        public static int Reset(int detr_index)
        {
            return CareRayApi.CrResetDetector(detr_index, 0);
        }
        public static int Disconnect(int detr_index)
        {
            return CareRayApi.CrDisconnect(detr_index);
        }

        public static int GetSysInfo(int detr_index)
        {
            CrSystemInfo sys_info = new CrSystemInfo();
            int ret_code = CareRayApi.CrGetSystemInformation(detr_index, ref sys_info);
            if (ret_code < CR_MIN_ERROR_CODE)
            {
                Console.WriteLine("resolution = {0} x {1}.", sys_info.raw_img_width, sys_info.raw_img_height);
                Console.WriteLine("SW version = {0}.", sys_info.software_version);
                Console.WriteLine("FW version = {0}.", sys_info.firmware_version);
                Console.WriteLine("machine id = {0}.", sys_info.detr_machine_id);
            }
            else
            {
                Console.WriteLine("Get system info failed: error code = {0}, msg = {1}.", ret_code, GetLastErrorMsg());
            }
            return ret_code;
        }

        public static CrSupportModeList GetSupportModeList(int detr_index)
        {
            byte[] recv_cmd_buff = new byte[1024 * 16];
            CareRayApi.CrGetApplicationModeInJson(detr_index, ref recv_cmd_buff[0]);

            support_mode_list[detr_index] = JsonConvert.DeserializeObject<CrSupportModeList>(System.Text.Encoding.Default.GetString(recv_cmd_buff));
            return support_mode_list[detr_index];
        }
        public static int GenerateSampleCustomModeConfigFile(int detr_index)
        {
            do
            {
                string file_path = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Config",
                        CR_CUSTOM_MODE_FILE_PATH); 
                if (System.IO.File.Exists(file_path))
                {
                    Console.WriteLine("Custom mode config file already exist.");
                    Console.WriteLine("Wether to generate sample file again? (Y/N)");
                    if ("N" == Console.ReadLine().ToUpper())
                    {
                        break;
                    }
                }

                CareRayApi.CrGenerateSampleCustomModeConfigFile(detr_index);
                Console.WriteLine("Please modify \"{0}\" according to the actual needs.", CR_CUSTOM_MODE_FILE_PATH);
            } while (false);

            return 0;
        }
        public static int RegisterCustomModeFromConfigFile(int detr_index)
        {
            byte[] recv_cmd_buff = new byte[1024 * 64];
            CareRayApi.CrRegisterCustomModeFromConfigFile(detr_index, ref recv_cmd_buff[0]);

            reg_result_list[detr_index] = JsonConvert.DeserializeObject<CrRegModeResultList>(System.Text.Encoding.Default.GetString(recv_cmd_buff));
            return reg_result_list[detr_index].result_code;
        }
        public static CrCustomModeInfo GetModeInfoByAppModeKeyFromConfigFile(int detr_index, int app_mode_key)
        {
            custom_mode_list = JsonConvert.DeserializeObject<CrCustomModeInfoList>(File.ReadAllText(CR_CUSTOM_MODE_FILE_PATH));
            if (null == custom_mode_list)
            {
                return null;
            }

            foreach (CrCustomModeInfo custom_mode in custom_mode_list.custom_mode_infos)
            {
                if (app_mode_key == custom_mode.app_mode_key && detr_index == custom_mode.detr_index)
                {
                    return custom_mode;
                }
            }

            return null;
        }
        private static CrRegModeType GetRegModeType(int detr_index, int app_mode_key)
        {
            CrRegModeType reged_mode_type = CrRegModeType.REGED_FLUORO_MODE_TYPE;
            do
            {
                if (null == reg_result_list || 0 == reg_result_list[detr_index].register_results.Count())
                {
                    Console.WriteLine("Please register custom mode firstly.");
                    break;
                }

                foreach (CrRegModeResult reg_result in reg_result_list[detr_index].register_results)
                {
                    if (app_mode_key == reg_result.app_mode_key)
                    {
                        if (reg_result.register_result >= CR_MIN_ERROR_CODE)
                        {
                            Console.WriteLine("The custom mode was not registered successfully before.");
                        }
                        // fluoro mode
                        else if (reg_result.mode_desc.Contains("fluoroscopic")
                            || reg_result.mode_desc.Contains("panoramic")
                            || reg_result.mode_desc.Contains("fluorographic"))
                        {
                            reged_mode_type = CrRegModeType.REGED_FLUORO_MODE_TYPE;
                        }
                        // rad mode
                        else
                        {
                            reged_mode_type = CrRegModeType.REGED_RAD_MODE_TYPE;
                        }

                        break;
                    }
                }
            } while (false);

            return reged_mode_type;
        }
        public static void StartAcquisition(int detr_index)
        {
            Console.WriteLine("Please enter the app_mode_key for acquisition.");
            int app_mode_key = Convert.ToInt32(Console.ReadLine());
            CrRegModeType reg_mode_type = GetRegModeType(detr_index, app_mode_key);

            // fluoro mode
            if (CrRegModeType.REGED_FLUORO_MODE_TYPE == reg_mode_type)
            {
                StartFluoroAcquisition(detr_index, app_mode_key);
            }
            // rad mode
            else if (CrRegModeType.REGED_RAD_MODE_TYPE == reg_mode_type)
            {
                StartRadAcquisition(detr_index, app_mode_key);
            }
            // do not registered or registered failed
            else
            {
                Console.WriteLine("The app_mode_key was not registered successfully before.");
            }
        }
        public static int StopAcquisition(int detr_index)
        {
            return CareRayApi.CrStopAcquisition(detr_index);
        }
        private static int MonitorCalibration(int detr_index, int app_mode_key, int is_dark_calib, CrRegModeType mode_type)
        {
            Console.WriteLine("Press any key to terminate the calibration process!");

            int prev_frame_num = -1, exp_permitted_hint = 0, wait_permission_hint = 0;
            CrCalibProgress progress = new CrCalibProgress();
            do
            {
                if (Console.KeyAvailable)
                {
                    CareRayApi.CrStopCalibration(detr_index);
                    Console.ReadLine();
                    Console.WriteLine("Calibration was terminated!");
                    break;
                }

                byte[] recv_cmd_buff = new byte[1024 * 16];
                // query progress
                CareRayApi.CrGetCalibrationProgressInJson(detr_index, ref recv_cmd_buff[0]);
                progress = JsonConvert.DeserializeObject<CrCalibProgress>(System.Text.Encoding.Default.GetString(recv_cmd_buff));
                //PrintCalibrationProgress(progress);

                if (0 == is_dark_calib && CrRegModeType.REGED_RAD_MODE_TYPE == mode_type)
                {
                    if (CrExpStatus.CR_EXP_READY == (CrExpStatus)progress.exposure_status)
                    {
                        if (0 == exp_permitted_hint)
                        {
                            Console.WriteLine("Target value = {0}", progress.target_gray_value);
                            Console.WriteLine("Status Polled>>  Detector is ready for Rad Acquisition, press x-ray hand switch now...");
                            exp_permitted_hint++;
                        }
                    }
                    else if (CrExpStatus.CR_EXP_WAIT_PERMISSION == (CrExpStatus)progress.exposure_status)
                    {
                        if (0 == wait_permission_hint)
                        {
                            Console.WriteLine("Status Polled>> Detector is waiting for exposure permission...");

                            if (0 != CareRayApi.CrPermitExposure(detr_index))
                            {
                                break;
                            }

                            wait_permission_hint++;
                        }
                    }
                }

                if (progress.current_frame_num > prev_frame_num)
                {
                    if (0 == progress.total_frame_num)
                    {
                        Console.WriteLine("Calibration progress: prechecking...");
                    }
                    else
                    {
                        Console.Write("Calibration progress: {0} / {1}\r", progress.current_frame_num, progress.total_frame_num);
                    }

                    prev_frame_num = progress.current_frame_num;
                    exp_permitted_hint = 0;
                    wait_permission_hint = 0;
                }

                System.Threading.Thread.Sleep(10);
            } while (-1 == progress.progress_result);

            return progress.progress_result;
        }
        public static void DoDarkCalibration(int detr_index)
        {
            Console.WriteLine("Please enter the app_mode_key for dark calibration.");
            int app_mode_key = Convert.ToInt32(Console.ReadLine());
            CrRegModeType reg_mode_type = GetRegModeType(detr_index, app_mode_key);

            // fluoro mode
            if (CrRegModeType.REGED_UNKNOWN_MODE_TYPE == reg_mode_type)
            {
                Console.WriteLine("The app_mode_key was not registered successfully before.");
            }

            if (CareRayApi.CrStartDarkCalibration(detr_index, app_mode_key, 1, 0) < CR_MIN_ERROR_CODE)
            {
                MonitorCalibration(detr_index, app_mode_key, 1, reg_mode_type);
            }
        }
        public static void DoGainCalibration(int detr_index)
        {
            Console.WriteLine("Please enter the app_mode_key for gain calibration.");
            int app_mode_key = Convert.ToInt32(Console.ReadLine());
            CrRegModeType reg_mode_type = GetRegModeType(detr_index, app_mode_key);

            // fluoro mode
            if (CrRegModeType.REGED_UNKNOWN_MODE_TYPE == reg_mode_type)
            {
                Console.WriteLine("The app_mode_key was not registered successfully before.");
            }

            if (CareRayApi.CrStartGainCalibration(detr_index, app_mode_key) < CR_MIN_ERROR_CODE)
            {
                MonitorCalibration(detr_index, app_mode_key, 0, reg_mode_type);
            }
        }

        public static void PrintModeInfo(CrModeInfo mode_info)
        {
            Console.WriteLine("=====================================");
            Console.WriteLine("Mode ID      = {0}", mode_info.mode_id);
            Console.WriteLine("Image width  = {0}", mode_info.image_width);
            Console.WriteLine("Image Height = {0}", mode_info.image_height);
            Console.WriteLine("Cutoff X     = {0}", mode_info.cutoff_x);
            Console.WriteLine("Cutoff Y     = {0}", mode_info.cutoff_y);
            Console.WriteLine("Max FPS      = {0}", mode_info.max_frame_rate);
            Console.WriteLine("Max exposure = {0}", mode_info.max_exposure_time);
            Console.Write("Trigger list = ");
            foreach (int trigger_type in mode_info.trigger_types)
            {
                Console.Write("{0} ", trigger_type);
            }
            Console.WriteLine();

            Console.Write("Gains list   = ");
            foreach (int gain_level in mode_info.gain_levels)
            {
                Console.Write("{0} ", gain_level);
            }
            Console.WriteLine();

            Console.WriteLine("Description  = {0}", mode_info.desc);
        }
        public static void PrintRegModeResultList(int detr_index)
        {
            foreach (CrRegModeResult reg_result in reg_result_list[detr_index].register_results)
            {
                Console.WriteLine("Register app_mode_key = {0}, ret_code = {1}, ret_desc = {2}",
                    reg_result.app_mode_key, reg_result.register_result, reg_result.register_desc);
            }
        }
        public static void PrintAcquisitionProgress(CrAcquisitionProgress progress)
        {
            Console.WriteLine("ret_code = {0}, exp_stat = {1}, fetchable_flag = {2}, progress_result = {3}",
                progress.result_code, progress.exposure_status, progress.is_fetchable, progress.progress_result);
        }
        public static void PrintCalibrationProgress(CrCalibProgress progress)
        {
            Console.WriteLine("ret_code = {0}, total_frame_num = {1}, current_frame_num = {2}, exposure_status = {3}, progress_result = {4}",
                progress.result_code, progress.total_frame_num, progress.current_frame_num, progress.exposure_status, progress.progress_result);
        }
        private static int StartAcquisitionWithCorrOpt(int detr_index, int app_mode_key, uint correct_options, int transfer_iamges_by_callback)
        {
            //uint demo_opt = (uint)(CrProcChainOpt.CR_PROCCHAIN_DARKCORR | CrProcChainOpt.CR_PROCCHAIN_GAINCORR
            //    | CrProcChainOpt.CR_PROCCHAIN_DEFECTCORR | CrProcChainOpt.CR_PROCCHAIN_ROTATE_IAMGE);
            //return CareRayApi.CrStartAcquisitionWithCorrOpt(detr_index, app_mode_key, demo_opt, transfer_iamges_by_callback);

            return CareRayApi.CrStartAcquisitionWithCorrOpt(detr_index, app_mode_key, correct_options, transfer_iamges_by_callback);
        }
        // 采集图像
        public static int acquisitionFrameCount = 1;
        public static List<WriteableBitmap> StartFluoroAcquisition(int detr_index, int app_mode_key)
        {
            var acquiredBitmaps = new List<WriteableBitmap>();
            int ret_code = 0;
            const int CR_FLUORO_FRAME_HEADER_LENGTH = 52; // 透视帧头长度（根据实际情况调整）

            try
            {
                // 获取模式信息（需要此信息来创建位图）
                CrCustomModeInfo costom_mode_info = GetModeInfoByAppModeKeyFromConfigFile(detr_index, app_mode_key);
                int nFrameLength = costom_mode_info.image_width * costom_mode_info.image_height * 2 + CR_FLUORO_FRAME_HEADER_LENGTH;
                byte[] recv_img_buff = new byte[nFrameLength];

                // 启动非回调模式采集
                ret_code = CareRayApi.CrStartAcquisition(detr_index, app_mode_key, 0);

                if (ret_code > CR_MIN_ERROR_CODE)
                {
                    Console.WriteLine("启动采集失败: 错误码 = {0}, 消息 = {1}.", ret_code, GetLastErrorMsg());
                    return acquiredBitmaps;
                }
                // 获取图像帧
                for (int round = 0; round < acquisitionFrameCount; round++)
                {
                    ret_code = CareRayApi.CrGetImage(detr_index, ref recv_img_buff[0], nFrameLength, 3000);
                    if (ret_code != 0)
                    {
                        Console.WriteLine("获取图像失败, 错误码 = {0}, 消息 = {1}", ret_code, GetLastErrorMsg());
                        continue; // 跳过失败帧
                    }
                    // 转换为WriteableBitmap
                    var bitmap = ConvertFluoroToBitmap(recv_img_buff, costom_mode_info.image_width, costom_mode_info.image_height);

                    if (bitmap != null)
                    {
                        acquiredBitmaps.Add(bitmap);
                        Console.WriteLine($"成功转换透视帧 #{round + 1}, 帧头编号: {BitConverter.ToInt32(recv_img_buff, 8)}");
                    }
                    else
                    {
                        Console.WriteLine($"转换透视帧 #{round + 1} 失败");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"透视采集异常: {ex.Message}");
            }
            finally
            {
                // 确保停止采集
                ret_code = CareRayApi.CrStopAcquisition(detr_index);
                Console.WriteLine("停止采集结果: {0}", ret_code);
            }
            return acquiredBitmaps;
        }
        private static WriteableBitmap ConvertFluoroToBitmap(byte[] frameData, int width, int height)
        {
            try
            {
                const int HEADER_LENGTH = 52; // 透视图像帧头长度
                const int PIXEL_SIZE = 2;     // 每个像素2字节

                // 验证数据长度
                int expectedLength = HEADER_LENGTH + (width * height * PIXEL_SIZE);
                if (frameData.Length < expectedLength)
                {
                    Console.WriteLine($"图像数据长度无效 ({frameData.Length})，预期 {expectedLength}");
                    return null;
                }
                // 创建16位灰度图像
                var bitmap = new WriteableBitmap(
                    width,
                    height,
                    96, // DPI X
                    96, // DPI Y
                    PixelFormats.Gray16,  // 16位灰度格式
                    null);
                // 锁定位图进行操作
                bitmap.Lock();

                try
                {
                    // 计算像素数据的大小
                    int pixelDataLength = width * height * PIXEL_SIZE;

                    // 从帧数据中提取像素数据（跳过帧头）
                    var sourceBuffer = new byte[pixelDataLength];
                    Buffer.BlockCopy(frameData, HEADER_LENGTH, sourceBuffer, 0, pixelDataLength);

                    // 修正字节顺序（如果需要）
                    FixByteOrderFor16BitGray(sourceBuffer);

                    // 复制数据到位图后台缓冲区
                    System.Runtime.InteropServices.Marshal.Copy(
                        sourceBuffer,
                        0,
                        bitmap.BackBuffer,
                        pixelDataLength);

                    // 标记整个位图为脏区（需要更新）
                    bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                }
                finally
                {
                    bitmap.Unlock();
                }

                return bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"图像转换失败: {ex}");
                return null;
            }
        }
        // 修正16位灰度图的字节顺序（如果需要）
        private static void FixByteOrderFor16BitGray(byte[] pixelData)
        {
            // 如果系统是小端序但图像数据是大端序，需要交换字节
            if (BitConverter.IsLittleEndian)
            {
                for (int i = 0; i < pixelData.Length; i += 2)
                {
                    byte temp = pixelData[i];
                    pixelData[i] = pixelData[i + 1];
                    pixelData[i + 1] = temp;
                }
            }
        }
        public static (int retCode, WriteableBitmap bitmap) StartRadAcquisition(int detr_index, int app_mode_key)
        {
            int ret_code = 0;
            int exp_permitted_hint = 0;
            int wait_permission_hint = 0;
            WriteableBitmap resultBitmap = null;
            try
            {
                // ... [之前的代碼保持不變，直到獲取圖像部分] ...
                ret_code = CareRayApi.CrStartAcquisition(detr_index, app_mode_key, 0);
                if (ret_code > CR_MIN_ERROR_CODE)
                {
                    Console.WriteLine("Start acquisition failed: error code = {0}, msg = {1}.", ret_code, GetLastErrorMsg());
                    return (ret_code, resultBitmap);
                }
                else
                {
                    Console.WriteLine("The result of CrStartAcquisition is {0}", ret_code);
                }

                CrCustomModeInfo costom_mode_info = GetModeInfoByAppModeKeyFromConfigFile(detr_index, app_mode_key);
                int nFrameLength = costom_mode_info.image_width * costom_mode_info.image_height * 2 + CR_RAD_FRAME_HEADER_LENGTH;
                byte[] recv_img_buff = new byte[nFrameLength];

                CrAcquisitionProgress progress = new CrAcquisitionProgress();

                while (0 == progress.is_fetchable)
                {
                    // query progress
                    CareRayApi.CrGetAcquisitionProgressInJson(detr_index, ref recv_img_buff[0]);
                    progress = JsonConvert.DeserializeObject<CrAcquisitionProgress>(System.Text.Encoding.Default.GetString(recv_img_buff));
                    PrintAcquisitionProgress(progress);

                    // query faild
                    if (progress.result_code >= CR_MIN_ERROR_CODE)
                    {
                        Console.WriteLine("Query exposure progress failed, code = {0}, desc = {1}", progress.result_code, progress.result_desc);
                        break;
                    }

                    if (CrExpStatus.CR_EXP_READY == (CrExpStatus)progress.exposure_status)
                    {
                        if (0 == exp_permitted_hint)
                        {
                            CareRayApi.CrRequestExposure(detr_index);
                            Console.WriteLine("Status Polled>>  Detector is ready for Rad Acquisition, press x-ray hand switch now...");
                            exp_permitted_hint++;
                        }
                    }
                    else if (CrExpStatus.CR_EXP_WAIT_PERMISSION == (CrExpStatus)progress.exposure_status)
                    {
                        if (0 == wait_permission_hint)
                        {
                            Console.WriteLine("Status Polled>> Detector is waiting for exposure permission...");

                            if (0 != CareRayApi.CrPermitExposure(detr_index))
                            {
                                break;
                            }

                            wait_permission_hint++;
                        }
                    }

                    System.Threading.Thread.Sleep(50);
                }
                // 準備獲取圖像
                if (0 != progress.is_fetchable)
                {
                    ret_code = CareRayApi.CrGetImage(detr_index, ref recv_img_buff[0], nFrameLength, 3000);
                    if (0 != ret_code)
                    {
                        Console.WriteLine("Get image failed, code = {0}, msg = {1}", ret_code, GetLastErrorMsg());
                    }
                    else
                    {
                        Console.WriteLine("Get a rad frame.");

                        // 將緩衝區轉換為 WriteableBitmap
                        resultBitmap = ConvertToWriteableBitmap(recv_img_buff, costom_mode_info);

                        if (resultBitmap == null)
                        {
                            Console.WriteLine("Failed to convert image data to bitmap");
                        }
                    }
                }
                else
                {
                    CareRayApi.CrStopAcquisition(detr_index);
                }

                return (ret_code, resultBitmap);
            }
            finally
            {
                // 確保總是停止採集（如果仍在進行）
                if (resultBitmap == null && ret_code == 0)
                {
                    CareRayApi.CrStopAcquisition(detr_index);
                }
            }
        }
        // 圖像轉換函數（添加到同一個類中）
        private static WriteableBitmap ConvertToWriteableBitmap(byte[] imageData, CrCustomModeInfo modeInfo)
        {
            try
            {
                // 常量定義（根據您的頭文件設定）
                const int CR_RAD_FRAME_HEADER_LENGTH = 40;  // 假設頭部長度為40字節
                const int HEADER_LENGTH_2 = 52;             // 另一種可能的頭部長度
                                                            // 創建 WriteableBitmap（16位灰度圖像）
                var bitmap = new WriteableBitmap(
                    modeInfo.image_width,
                    modeInfo.image_height,
                    96,  // DPI X
                    96,  // DPI Y
                    PixelFormats.Gray16,  // 每像素16位（2字節）灰度圖像
                    null);
                // 鎖定位圖緩衝區
                bitmap.Lock();

                try
                {
                    // 獲取圖像數據的起始位置（跳過頭部）
                    int dataStartIndex = CR_RAD_FRAME_HEADER_LENGTH;

                    // 處理特殊情況：如果數據長度不匹配，嘗試另一種頭部長度
                    if (imageData.Length != CR_RAD_FRAME_HEADER_LENGTH + (modeInfo.image_width * modeInfo.image_height * 2))
                    {
                        dataStartIndex = HEADER_LENGTH_2;
                    }
                    // 計算實際圖像數據長度
                    int actualPixelDataSize = modeInfo.image_width * modeInfo.image_height * 2;

                    // 確保數據長度正確
                    if (imageData.Length < dataStartIndex + actualPixelDataSize)
                    {
                        Debug.WriteLine($"Invalid image data length: {imageData.Length}, expected at least {dataStartIndex + actualPixelDataSize}");
                        return null;
                    }
                    // 複製圖像數據到位圖緩衝區
                    var sourceBuffer = new byte[actualPixelDataSize];
                    Buffer.BlockCopy(imageData, dataStartIndex, sourceBuffer, 0, actualPixelDataSize);

                    // 使用 Marshal 複製數據（確保高性能）
                    var destPtr = bitmap.BackBuffer;
                    System.Runtime.InteropServices.Marshal.Copy(sourceBuffer, 0, destPtr, actualPixelDataSize);
                    // 設定圖像刷新區域
                    bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));

                    return bitmap;
                }
                finally
                {
                    // 無論成功與否都解鎖位圖
                    bitmap.Unlock();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bitmap creation failed: {ex}");
                return null;
            }
        }

    }
}
