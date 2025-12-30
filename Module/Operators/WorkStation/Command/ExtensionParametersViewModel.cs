using Core.Abstraction;
using Core.Utilities;
using Framework.Models;
using ModuleCore.Common.Authority;
using ModuleCore.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Stations;
using Stations.Event;
using Stations.Models;
using Stations.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Module.ViewModels
{
    public class ExtensionParametersViewModel : BindableBase
    {
        private LoginModel _loginModel { get; set; }
        private DispenserStation _dispenserStation;
        private TaskInstanceManager _taskManager;
        private ObservableCollection<ExtensionParameter> _parameters;
        private bool _isRefreshing;
        private string _refreshStatus;
        private string _cachedVisionData;
        private DateTime _visionDataTimestamp = DateTime.MinValue;

        public ObservableCollection<ExtensionParameter> Parameters
        {
            get => _parameters;
            set => SetProperty(ref _parameters, value);
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        public string RefreshStatus
        {
            get => _refreshStatus;
            set => SetProperty(ref _refreshStatus, value);
        }

        public DelegateCommand Perform3DScanCommand { get; private set; }
        public DelegateCommand RefreshAllCommand { get; private set; }
        public DelegateCommand<ExtensionParameter> RefreshSingleCommand { get; private set; }
        public DelegateCommand SaveParametersCommand { get; private set; }
        public DelegateCommand LoadParametersCommand { get; private set; }

        private readonly ILoggerService _logger;
        private readonly ICameraController _cameraController;
        private readonly IVisionDataService _visionDataService;
        private readonly IParameterStorage _parameterStorage;
        private readonly IEventAggregator _eventAggregator;
        private readonly ICompensationService _compensationService;    // 补偿服务
        private readonly IH2HeightDataService _h2HeightDataService;
        private readonly ICancelableOperationService _cancelableOperationService;

        public ExtensionParametersViewModel(
            ILoggerService logger,
            ICameraController cameraController,
            IVisionDataService visionDataService,
            IParameterStorage parameterStorage,
            IEventAggregator eventAggregator,
            ICompensationService compensationService,
            IH2HeightDataService h2HeightDataService,
            ICancelableOperationService cancelableOperationService,
            TaskInstanceManager taskManager)
        {
            _logger = logger;
            _cameraController = cameraController;
            _visionDataService = visionDataService;
            _parameterStorage = parameterStorage;
            _eventAggregator = eventAggregator;
            _compensationService = compensationService;
            _h2HeightDataService = h2HeightDataService;
            _cancelableOperationService = cancelableOperationService;
            _dispenserStation = taskManager.GetTask<DispenserStation>();
            InitializeParameters();
            InitializeCommands();
            LoadStoredParameters();
        }

        private void InitializeParameters()
        {
            Parameters = new ObservableCollection<ExtensionParameter>();

            // 初始化6个tab的参数
            for (int i = 1; i <= 6; i++)
            {
                Parameters.Add(new ExtensionParameter
                {
                    Index = i,
                    ReferenceHeight = 10.0,  // 默认基准高度
                    UpperLimit = 12.0,       // 默认上限
                    LowerLimit = 8.0,        // 默认下限
                    Compensation = 0.0       // 默认补偿
                });
            }
        }

        private void InitializeCommands()
        {
            RefreshAllCommand = new DelegateCommand(async () => await RefreshAllHeightsAsync());
            RefreshSingleCommand = new DelegateCommand<ExtensionParameter>(async param => await RefreshSingleHeightAsync(param));
            SaveParametersCommand = new DelegateCommand(SaveParameters);
            LoadParametersCommand = new DelegateCommand(LoadParameters);
        }

        private void LoadStoredParameters()
        {
            try
            {
                string _customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                       "Config",
                                       "Parameters");
                var storedParams = _parameterStorage?.Load<ExtensionParameters>("ExtensionParameters", _customDirectory);
                if (storedParams != null && storedParams.Parameters != null)
                {
                    foreach (var storedParam in storedParams.Parameters)
                    {
                        var param = Parameters.FirstOrDefault(p => p.Index == storedParam.Index);
                        if (param != null)
                        {
                            param.ReferenceHeight = storedParam.ReferenceHeight;
                            param.UpperLimit = storedParam.UpperLimit;
                            param.LowerLimit = storedParam.LowerLimit;
                            param.Compensation = storedParam.Compensation;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RefreshStatus = $"加载参数失败: {ex.Message}";
            }
        }

        private void SaveParameters()
        {
            try
            {
                string _customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                       "Config",
                       "Parameters");
                var extensionParams = new ExtensionParameters
                {
                    Parameters = Parameters.ToList()
                };

                _parameterStorage?.Save("ExtensionParameters", extensionParams, _customDirectory);
                RefreshStatus = "参数保存成功";
            }
            catch (Exception ex)
            {
                RefreshStatus = $"保存参数失败: {ex.Message}";
            }
        }

        private void LoadParameters()
        {
            LoadStoredParameters();
        }

        public async Task RefreshSingleHeightAsync(ExtensionParameter parameter)
        {
            if (parameter == null) return;

            try
            {
                RefreshStatus = $"正在更新Tab{parameter.Index}的高度...";

                if (string.IsNullOrEmpty(_cachedVisionData))
                {
                    RefreshStatus = "缓存数据已过期，重新拍照获取数据...";
                }
                else
                {
                    // 使用视觉数据解析指定Tab的高度
                    RefreshStatus = $"从缓存数据解析Tab{parameter.Index}的高度...";

                    try
                    {
                        double height = ParseHeightFromVisionData(_cachedVisionData, parameter.Index);
                        parameter.RealTimeHeight = height;
                        // 补偿值 = 基准高度 - 实时高度
                        parameter.Compensation = Math.Round(parameter.ReferenceHeight - height, 3);
                        RefreshStatus = $"Tab{parameter.Index}高度更新完成: {height:F3}mm, 补偿值: {parameter.Compensation:F3}mm";

                        // 发布H2Height更新事件
                        double h2Height = parameter.H2Height; // H2Height属性会自动计算
                        _eventAggregator.GetEvent<H2HeightUpdatedEvent>()
                            .Publish(new H2HeightData
                            {
                                TabIndex = parameter.Index,
                                H2Height = h2Height,
                                Timestamp = DateTime.Now
                            });

                        RefreshStatus = $"Tab{parameter.Index}高度更新完成: {height:F3}mm, 补偿值: {parameter.Compensation:F3}mm, H2Height: {h2Height:F3}mm";
                        _logger?.Info($"发布Tab{parameter.Index}的H2Height: {h2Height:F3}mm");
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warn($"从缓存解析Tab{parameter.Index}高度失败: {ex.Message}");
                        RefreshStatus = $"缓存解析失败，重新拍照获取Tab{parameter.Index}高度...";
                    }
                }
            }
            catch (Exception ex)
            {
                RefreshStatus = $"更新Tab{parameter.Index}失败: {ex.Message}";
                parameter.RealTimeHeight = double.NaN;
            }
        }

        private async Task<bool> RefreshAllHeightsAsync()
        {
            try
            {
                string cameraName = "3DCAMERA";
                string photoCommand = $"H1-H6"; // H1-H6对应高度拍照

                int timeout = 30000; // 30秒

                // 1. 先执行3D扫描
                Task scanTask = null;
                if (_dispenserStation != null && _logger != null)
                {
                    RefreshStatus = $"正在执行3D扫描...";

                    // 启动3D扫描任务
                    scanTask = Task.Run(async () =>
                    {
                        try
                        {
                            await Perform3DScanAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error($"3D扫描失败: {ex.Message}");
                        }
                    });
                }

                // 2. 延迟2秒，等待3D扫描稳定
                RefreshStatus = $"等待3D扫描稳定...";
                await Task.Delay(2000); // 延迟2秒

                // 3. 启动视觉数据等待任务
                RefreshStatus = $"等待视觉数据...";

                var visionTask = Task.Run(async () =>
                    await _visionDataService.WaitForVisionDataAsync(cameraName, timeout));

                // 给视觉系统一点时间建立连接
                await Task.Delay(100);

                // 4. 触发拍照
                RefreshStatus = $"正在拍照获取所有Tab高度...";

                bool photoSuccess = await Task.Run(() =>
                    _cameraController.TakePhotoAsync(cameraName, photoCommand));

                if (!photoSuccess)
                {
                    throw new Exception("拍照失败");
                }

                // 5. 等待视觉数据
                RefreshStatus = $"正在处理视觉数据...";

                string visionData = await visionTask;

                if (string.IsNullOrEmpty(visionData))
                {
                    throw new Exception("视觉数据为空");
                }

                // 6. 缓存视觉数据
                _cachedVisionData = visionData;
                _visionDataTimestamp = DateTime.Now;

                // 7. 解析所有Tab的高度（1-6）
                RefreshStatus = $"解析所有Tab高度...";
                ParseAndUpdateAllHeights(visionData);

                // 8. 等待3D扫描完成
                if (scanTask != null)
                {
                    try
                    {
                        // 不等待3D扫描完成，继续执行，但记录状态
                        _ = scanTask.ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                _logger?.Warn($"3D扫描失败: {t.Exception?.Message}");
                            }
                            else if (t.IsCompletedSuccessfully)
                            {
                                _logger?.Info($"3D扫描完成");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warn($"启动3D扫描任务异常: {ex.Message}");
                    }
                }

                RefreshStatus = "所有Tab高度更新完成";
                return true;
            }
            catch (TimeoutException)
            {
                // 设置所有Tab的高度为NaN
                foreach (var param in Parameters)
                {
                    param.RealTimeHeight = double.NaN;
                }
                //throw new Exception("视觉数据获取超时");
                _logger?.Warn($"视觉数据获取超时");
                return false;
            }
            catch (OperationCanceledException)
            {
                foreach (var param in Parameters)
                {
                    param.RealTimeHeight = double.NaN;
                }
                //throw new Exception("操作被取消");
                _logger?.Warn($"操作被取消");
                RefreshStatus = "操作被取消";
                return false;
            }
            catch (Exception ex)
            {
                foreach (var param in Parameters)
                {
                    param.RealTimeHeight = double.NaN;
                }
                //throw new Exception($"获取高度失败: {ex.Message}");
                _logger?.Warn($"获取高度失败: {ex.Message}");
                RefreshStatus = $"获取高度失败: {ex.Message}";
                return false;
            }
        }

        // 解析并更新所有Tab的高度（1-6）
        private void ParseAndUpdateAllHeights(string visionData)
        {
            try
            {
                // 数据格式：Camera=3DCamera;VISION_RESULT:SUCCESS:123.1,19.3,123.2,19.2,123.0,19.2,123.7,19.0,123.0,19.9,122.9,18.9
                // 解析逻辑：VISION_RESULT:SUCCESS:后面有12个数值
                // 前6个：H1, H2, H3, H4, H5, H6
                // 后6个：H11, H12, H13, H14, H15, H16
                // Tab1高度 = H1 - H11
                // Tab2高度 = H2 - H12
                // Tab3高度 = H3 - H13
                // Tab4高度 = H4 - H14
                // Tab5高度 = H5 - H15
                // Tab6高度 = H6 - H16

                // 解析所有数值
                var allValues = ParseAllValuesFromVisionData(visionData);

                // 检查是否有足够的数据（需要12个值）
                if (allValues.Count < 12)
                {
                    _logger?.Warn($"视觉数据不足12个值，实际{allValues.Count}个");
                    return;
                }

                // 计算并更新每个Tab的高度
                for (int i = 0; i < 6; i++)
                {
                    try
                    {
                        // 前6个值：H1-H6
                        double hValue = allValues[i];
                        // 后6个值：H11-H16
                        double h11Value = allValues[i + 6];

                        // 计算Tab高度：Hn - H(n+10)
                        double tabHeight = hValue - h11Value;

                        // 找到对应的参数对象（索引从1开始）
                        var parameter = Parameters.FirstOrDefault(p => p.Index == i + 1);
                        if (parameter != null)
                        {
                            parameter.RealTimeHeight = Math.Round(tabHeight, 3);
                            // 计算补偿值（实时高度减去参考高度）
                            parameter.Compensation = Math.Round(parameter.ReferenceHeight - tabHeight, 3);

                            // 更新补偿服务
                            _compensationService.UpdateCompensation(parameter.Index, CompensationType.Tab,
                                   new CompensationData
                                   {
                                       CompensationZ = parameter.Compensation,
                                       CompensationX = 0,
                                       CompensationY = 0
                                   });

                            // 计算H2Height并发布事件
                            double h2Height = parameter.H2Height; // H2Height属性会自动计算

                            // 发布H2Height更新事件
                            _eventAggregator.GetEvent<H2HeightUpdatedEvent>()
                                .Publish(new H2HeightData
                                {
                                    TabIndex = parameter.Index,
                                    H2Height = h2Height,
                                    Timestamp = DateTime.Now
                                });
                            _h2HeightDataService.UpdateH2Height(parameter.Index, h2Height);
                            _logger?.Info($"发布Tab{parameter.Index}的H2Height: {h2Height:F3}mm");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warn($"计算Tab{i + 1}高度失败: {ex.Message}");
                        // 设置该Tab高度为NaN
                        var parameter = Parameters.FirstOrDefault(p => p.Index == i + 1);
                        if (parameter != null)
                        {
                            parameter.RealTimeHeight = double.NaN;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"解析所有Tab高度失败: {ex.Message}");
                // 可以选择抛出异常或静默处理
                // throw;
            }
        }

        private List<double> ParseAllValuesFromVisionData(string visionData)
        {
            List<double> values = new List<double>();

            try
            {
                // 查找视觉结果部分
                if (string.IsNullOrEmpty(visionData))
                {
                    _logger?.Warn("视觉数据为空");
                    return values;
                }

                // 查找VISION_RESULT:SUCCESS:部分
                int startIndex = visionData.IndexOf("VISION_RESULT:SUCCESS:");
                if (startIndex < 0)
                {
                    _logger?.Warn("未找到VISION_RESULT:SUCCESS:标记");
                    return values;
                }

                // 提取偏移数据部分
                string offsetData = visionData.Substring(startIndex + "VISION_RESULT:SUCCESS:".Length);

                // 分割成数值
                string[] valueStrings = offsetData.Split(',');

                // 解析所有数值
                foreach (string valueStr in valueStrings)
                {
                    if (double.TryParse(valueStr, out double value))
                    {
                        values.Add(value);
                    }
                    else
                    {
                        _logger?.Warn($"无法解析数值: {valueStr}");
                        values.Add(double.NaN);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"解析视觉数据失败: {ex.Message}");
                // 返回空列表或部分解析结果
            }

            return values;
        }

        // 解析单个Tab的高度
        private double ParseHeightFromVisionData(string visionData, int index)
        {
            try
            {
                // 检查索引范围
                if (index < 1 || index > 6)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), "索引必须在1-6之间");
                }

                // 解析所有数值
                var allValues = ParseAllValuesFromVisionData(visionData);

                // 检查是否有足够的数据
                if (allValues.Count < 12)
                {
                    throw new Exception($"视觉数据不足12个值，实际{allValues.Count}个");
                }

                // 计算指定Tab的高度：Hn - H(n+10)
                // 注意：索引从1开始，而列表索引从0开始
                int n = index - 1;  // 转换为0-based索引

                double hValue = allValues[n];
                double h11Value = allValues[n + 6];
                double tabHeight = hValue - h11Value;

                return Math.Round(tabHeight, 3);
            }
            catch (Exception ex)
            {
                _logger?.Warn($"解析Tab{index}高度失败: {ex.Message}");
                throw; // 或者返回 double.NaN
            }
        }

        private async Task Perform3DScanAsync()
        {
            // 初始化 DispenserStationService
            var dispenserStationService = new DispenserStationService(_dispenserStation, _logger);

            bool success = await _cancelableOperationService.ExecuteWithCancellationAsync(
                title: "3D扫描",
                message: "正在执行3D扫描，请稍候...",
                operation: async (cancellationToken, progress, statusProgress) =>
                {
                    try
                    {
                        // 立即开始执行
                        RefreshStatus = "扫描中";
                        statusProgress.Report("初始化扫描设备...");
                        progress.Report(0);

                        // 创建进度回调
                        var progressHandler = new Progress<(int progress, string status)>(report =>
                        {
                            progress.Report((double)report.progress);
                            statusProgress.Report(report.status);

                            if (!string.IsNullOrEmpty(report.status))
                            {
                                RefreshStatus = report.status + " " + report.progress + "%";
                            }
                        });
                        // 1. 移动到扫描位置
                        RefreshStatus = "正在移动到扫描位置...";
                        statusProgress.Report("移动平台到扫描位置");
                        progress.Report(10);

                        bool moveSuccess = await Task.Run(async () =>
                        {
                            return await _dispenserStation.PlatMoveToScanPositionAsync();
                        }, cancellationToken);

                        if (!moveSuccess)
                        {
                            RefreshStatus = "移动到扫描位置失败";
                            statusProgress.Report("移动失败");
                            return false;
                        }

                        // 2. 执行3D扫描
                        RefreshStatus = "正在执行3D扫描...";
                        statusProgress.Report("执行3D扫描");
                        progress.Report(30);
                        // 使用 Task.Run 包装同步的扫描操作
                        bool scanSuccess = await Task.Run(() =>
                        {
                            // 注册取消回调
                            using (cancellationToken.Register(() =>
                            {
                                try
                                {
                                    RefreshStatus = "正在停止扫描...";
                                    dispenserStationService.CancelCurrentOperation();
                                }
                                catch (Exception ex)
                                {
                                    RefreshStatus = $"停止扫描时发生异常: {ex.Message}"; 
                                }
                            }))
                            {
                                // 使用 cancellationToken 传递取消信号
                                return _dispenserStation.Perform3DScanAsync(cancellationToken, progressHandler);
                            }
                        }, cancellationToken);

                        // 在长时间运行的操作中定期检查取消
                        cancellationToken.ThrowIfCancellationRequested();

                        if (scanSuccess)
                        {
                            RefreshStatus = "3D扫描完成";
                            statusProgress.Report("扫描完成");
                            progress.Report(100);
                        }
                        else
                        {
                            RefreshStatus = "3D扫描失败";
                            statusProgress.Report("扫描失败");
                        }

                        return scanSuccess;
                    }
                    catch (OperationCanceledException)
                    {
                        RefreshStatus = "3D扫描操作被取消";
                        statusProgress.Report("操作已取消");
                        return false;
                    }
                    catch (Exception ex)
                    {
                        RefreshStatus = $"3D扫描异常: {ex.Message}";
                        statusProgress.Report($"扫描异常: {ex.Message}");
                        return false;
                    }
                },
                showProgress: true,
                showStatus: true
            );

            if (!success)
            {
                RefreshStatus = "3D扫描操作被取消或失败";
            }
        }

        // 工站类调用接口
        public ExtensionParameter GetParameter(int index)
        {
            return Parameters.FirstOrDefault(p => p.Index == index);
        }

        public double GetCompensation(int index)
        {
            var param = GetParameter(index);
            return param?.Compensation ?? 0.0;
        }

        public bool ValidateHeight(int index, double height)
        {
            var param = GetParameter(index);
            if (param == null) return false;

            return height >= param.LowerLimit && height <= param.UpperLimit;
        }

    }

    // 参数存储类
    public class ExtensionParameters
    {
        public List<ExtensionParameter> Parameters { get; set; }
    }
}