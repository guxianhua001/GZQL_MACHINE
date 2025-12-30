using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Interfaces.Services
{
    public class OperationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public static OperationResult Ok() => new OperationResult { Success = true };
        public static OperationResult Fail(string message) => new OperationResult
        {
            Success = false,
            ErrorMessage = message
        };
    }

    public class OperationResult<T> : OperationResult
    {
        public T Data { get; set; }

        public static OperationResult<T> SuccessResult(T data) => new OperationResult<T>
        {
            Success = true,
            Data = data
        };

        public static new OperationResult<T> Fail(string message) => new OperationResult<T>
        {
            Success = false,
            ErrorMessage = message
        };
    }
    public class CareRayService
    {
        private Dictionary<int, bool> _deviceStatus = new Dictionary<int, bool>()
        {
            {1, false}, // 平板1#初始未连接
            {2, false}  // 平板2#
        };

        private Dictionary<int, List<CrModeInfo>> _deviceModes = new Dictionary<int, List<CrModeInfo>>()
        {
            {1, new List<CrModeInfo>()},
            {2, new List<CrModeInfo>()}
        };
        private readonly Dictionary<int, bool> _connectionStatus = new Dictionary<int, bool>
        {
            {1, false},
            {2, false}
        };
        // 检查设备是否已连接
        public bool IsConnected(int deviceId)
        {
            return _connectionStatus.ContainsKey(deviceId) && _connectionStatus[deviceId];
        }
        private readonly Dictionary<int, List<CrModeInfo>> _supportedModes = new Dictionary<int, List<CrModeInfo>>
        {
            {1, new List<CrModeInfo>()},
            {2, new List<CrModeInfo>()}
        };

        // 加载支持的模式
        private void LoadSupportedModes(int deviceId)
        {
            try
            {
                var modeList = CareRayOperator.GetSupportModeList(deviceId);
                if (modeList?.mode_infos != null && modeList.result_code == 0)
                {
                    _supportedModes[deviceId] = modeList.mode_infos;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载模式失败 (平板 {deviceId}): {ex.Message}");
            }
        }
        // 从配置文件注册自定义模式
        private void RegisterCustomModeFromConfigFile(int deviceId)
        {
            try
            {
                CareRayOperator.RegisterCustomModeFromConfigFile(deviceId);
                CareRayOperator.PrintRegModeResultList(deviceId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"注册自定义模式失败 (平板 {deviceId}): {ex.Message}");
            }
        }
        // 获取指定设备的支持模式
        public IReadOnlyList<CrModeInfo> GetSupportedModes(int deviceId)
        {
            if (_supportedModes.ContainsKey(deviceId))
            {
                return _supportedModes[deviceId].AsReadOnly();
            }
            return new List<CrModeInfo>();
        }
        public CareRayService()
        {
            Initialize();
        }

        // 初始化库
        private bool Initialize()
        {
            try
            {
                int result = CareRayOperator.InitLibrary();
                if (result < CareRayOperator.CR_MIN_ERROR_CODE)
                {
                    Console.WriteLine("平板库初始化成功");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                string error = CareRayOperator.GetLastErrorMsg();
                Console.WriteLine($"平板初始化失败：{error}");
                return false;
            }
        }

        // 连接到设备
        public async Task<OperationResult> ConnectAsync(int deviceId)
        {
            if (!_deviceStatus.ContainsKey(deviceId))
            {
                return OperationResult.Fail($"无效的设备ID: {deviceId}");
            }

            if (_deviceStatus[deviceId])
            {
                return OperationResult.Ok(); // 已经连接
            }

            try
            {
                return await Task.Run(() =>
                {
                    int result = CareRayOperator.Connect(deviceId);
                    if (result < CareRayOperator.CR_MIN_ERROR_CODE)
                    {
                        _deviceStatus[deviceId] = true;
                        _connectionStatus[deviceId] = true;
                        LoadSupportedModes(deviceId);
                        RegisterCustomModeFromConfigFile(deviceId);
                        return OperationResult.Ok();
                    }

                    string error = CareRayOperator.GetLastErrorMsg();
                    return OperationResult.Fail($"平板{deviceId}连接失败：{error}");
                });
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"连接时发生异常：{ex.Message}");
            }
        }
        // 调试取图
        public async Task<OperationResult<List<WriteableBitmap>>> FetchDebugImages(int deviceId, int appModeKey, int frameCount = 5)
        {
            try
            {
                if (!IsConnected(deviceId))
                {
                    return OperationResult<List<WriteableBitmap>>.Fail($"平板{deviceId}未连接");
                }

                // 自动调整帧数限制
                frameCount = Math.Clamp(frameCount, 1, 20);

                var result = await Task.Run(() =>
                {
                    try
                    {
                        CareRayOperator.acquisitionFrameCount = frameCount;
                        var images = CareRayOperator.StartFluoroAcquisition(deviceId, appModeKey);
                        return OperationResult<List<WriteableBitmap>>.SuccessResult(images);
                    }
                    catch (Exception ex)
                    {
                        return OperationResult<List<WriteableBitmap>>.Fail($"图像采集失败：{ex.Message}");
                    }
                    finally
                    {
                        // 确保停止采集
                        // CareRayOperator.CrStopAcquisition(deviceId);
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                return OperationResult<List<WriteableBitmap>>.Fail($"调试取图时发生异常：{ex.Message}");
            }
        }

        // 采集图像
        public async Task<OperationResult<WriteableBitmap>> CaptureImage(int deviceId, int appModeKey)
        {
            try
            {
                if (!IsConnected(deviceId))
                {
                    return OperationResult<WriteableBitmap>.Fail($"平板{deviceId}未连接");
                }

                var modeType = GetModeType(deviceId, appModeKey);

                return await Task.Run(() =>
                {
                    try
                    {
                        if (modeType == CareRayOperator.CrRegModeType.REGED_FLUORO_MODE_TYPE)
                        {
                            // 透视模式
                            CareRayOperator.acquisitionFrameCount = 1;
                            var frames = CareRayOperator.StartFluoroAcquisition(deviceId, appModeKey);

                            if (frames?.Count > 0)
                            {
                                return OperationResult<WriteableBitmap>.SuccessResult(frames[0]);
                            }

                            return OperationResult<WriteableBitmap>.Fail("未获取到任何图像帧");
                        }
                        else
                        {
                            // 放射模式
                            var result = CareRayOperator.StartRadAcquisition(deviceId, appModeKey);

                            if (result.retCode == 0)
                            {
                                return OperationResult<WriteableBitmap>.SuccessResult(result.bitmap);
                            }

                            string error = CareRayOperator.GetLastErrorMsg();
                            return OperationResult<WriteableBitmap>.Fail($"放射模式采图失败：{error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        return OperationResult<WriteableBitmap>.Fail($"图像采集过程中发生错误：{ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                return OperationResult<WriteableBitmap>.Fail($"拍图操作异常：{ex.Message}");
            }
        }
        // 获取模式类型
        public CareRayOperator.CrRegModeType GetModeType(int deviceId, int appModeKey)
        {
            if (!_supportedModes.ContainsKey(deviceId))
                return CareRayOperator.CrRegModeType.REGED_RAD_MODE_TYPE;

            foreach (var mode in _supportedModes[deviceId])
            {
                if (mode.mode_id == appModeKey)
                {
                    if ((mode.desc?.Contains("fluoroscopic") ?? false) ||
                        (mode.desc?.Contains("panoramic") ?? false) ||
                        (mode.desc?.Contains("fluorographic") ?? false))
                    {
                        return CareRayOperator.CrRegModeType.REGED_FLUORO_MODE_TYPE;
                    }
                    break;
                }
            }
            return CareRayOperator.CrRegModeType.REGED_RAD_MODE_TYPE;
        }
        // 停止采集图像
        public async Task<OperationResult> StopAcquisitionAsync(int deviceId)
        {
            try
            {
                if (!IsConnected(deviceId))
                    return OperationResult.Fail($"平板{deviceId}未连接");

                return await Task.Run(() =>
                {
                    int result = CareRayOperator.StopAcquisition(deviceId);
                    if (result < CareRayOperator.CR_MIN_ERROR_CODE)
                    {
                        return OperationResult.Ok();
                    }

                    string error = CareRayOperator.GetLastErrorMsg();
                    return OperationResult.Fail($"停止图像采集失败 (平板{deviceId}): {error}");
                });
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"停止采集时发生异常: {ex.Message}");
            }
        }
        // 断开连接
        public OperationResult Disconnect(int deviceId)
        {
            try
            {
                if (_deviceStatus.TryGetValue(deviceId, out bool connected) && connected)
                {
                    CareRayOperator.Disconnect(deviceId);
                    _deviceStatus[deviceId] = false;
                    _connectionStatus[deviceId] = false;
                    Console.WriteLine("平板已断开连接");
                    return OperationResult.Ok();
                }
                return OperationResult.Fail($"平板{deviceId}未连接或连接已关闭");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail($"断开连接时发生异常：{ex.Message}");
            }
        }

        ~CareRayService()
        {
            CareRayOperator.DeinitLibrary();
        }
    }
}
