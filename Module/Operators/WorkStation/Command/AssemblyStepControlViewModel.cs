using Core.Abstraction;
using Core.Utilities;
using MaterialDesignThemes.Wpf;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using SmarterMotion;
using Stations;
using Stations.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Module.ViewModels
{

    public class AssemblyStepControlViewModel : BindableBase
    {
        private readonly IParameterStorage _parameterStorage;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly ICompensationService _compensationService;    // 补偿服务
        //private readonly IMotionService _motionService;
        private readonly TaskInstanceManager _taskManager;
        private AssemblyStation _assemblyStation;

        private IAxis AssemblyX;
        private IAxis AssemblyZ;
        private IAxis AssemblyY;
        private IAxis AssemblyU;


        #region 属性定义

        // 单步控制属性
        private bool _isSingleStepMode = false;
        public bool IsSingleStepMode
        {
            get => _isSingleStepMode;
            set => SetProperty(ref _isSingleStepMode, value);
        }

        private string _currentStepDescription = "就绪";
        public string CurrentStepDescription
        {
            get => _currentStepDescription;
            set => SetProperty(ref _currentStepDescription, value);
        }

        private bool _isStepWaiting = false;
        public bool IsStepWaiting
        {
            get => _isStepWaiting;
            set => SetProperty(ref _isStepWaiting, value);
        }

        // 工位选择
        private ObservableCollection<int> _stationIndices = new ObservableCollection<int> { 1, 2, 3, 4, 5, 6 };
        public ObservableCollection<int> StationIndices
        {
            get => _stationIndices;
            set => SetProperty(ref _stationIndices, value);
        }

        private int _selectedStationIndex = 1;
        public int SelectedStationIndex
        {
            get => _selectedStationIndex;
            set
            {
                if (SetProperty(ref _selectedStationIndex, value))
                {
                    // 切换工位时自动加载对应的补偿值
                    LoadCompensationForStation(value);
                }
            }
        }

        // 补偿值属性
        private double _compensationX;
        public double CompensationX
        {
            get => _compensationX;
            set => SetProperty(ref _compensationX, value);
        }

        private double _compensationZ;
        public double CompensationZ
        {
            get => _compensationZ;
            set => SetProperty(ref _compensationZ, value);
        }

        private double _compensationY;
        public double CompensationY
        {
            get => _compensationY;
            set => SetProperty(ref _compensationY, value);
        }

        private double _compensationXTranslate;
        public double CompensationXTranslate
        {
            get => _compensationXTranslate;
            set => SetProperty(ref _compensationXTranslate, value);
        }
        private double _compensationZTranslate;
        public double CompensationZTranslate
        {
            get => _compensationZTranslate;
            set => SetProperty(ref _compensationZTranslate, value);
        }

        private double _compensationZPress;
        public double CompensationZPress
        {
            get => _compensationZPress;
            set => SetProperty(ref _compensationZPress, value);
        }

        // 状态属性
        private string _compensationStatus = "就绪";
        public string CompensationStatus
        {
            get => _compensationStatus;
            set => SetProperty(ref _compensationStatus, value);
        }

        private bool _isAssemblyRunning;
        public bool IsAssemblyRunning
        {
            get => _isAssemblyRunning;
            set => SetProperty(ref _isAssemblyRunning, value);
        }

        private string _assemblyStatus = "就绪";
        public string AssemblyStatus
        {
            get => _assemblyStatus;
            set => SetProperty(ref _assemblyStatus, value);
        }

        #endregion

        #region 命令定义

        public DelegateCommand StartSingleStepCommand { get; }
        public DelegateCommand NextStepCommand { get; }
        public DelegateCommand StopSingleStepCommand { get; }
        public DelegateCommand LoadCompensationCommand { get; }
        public DelegateCommand SaveCompensationCommand { get; }
        public DelegateCommand ApplyToAllStationsCommand { get; }
        public DelegateCommand ResetCompensationCommand { get; }
        public DelegateCommand StartAssemblyCommand { get; }
        public DelegateCommand MoveXRelativeCommand { get; }
        public DelegateCommand MoveZRelativeCommand { get; }
        public DelegateCommand MoveYRelativeCommand { get; }
        public DelegateCommand MoveXTranslateRelativeCommand { get; }
        public DelegateCommand MoveZPressRelativeCommand { get; }

        public DelegateCommand GetCompensationXCommand { get; }
        public DelegateCommand GetCompensationZCommand { get; }
        public DelegateCommand GetCompensationYCommand { get; }
        public DelegateCommand GetCompensationXTranslateCommand { get; }
        public DelegateCommand GetCompensationZPressCommand { get; }
        #endregion

        #region 构造函数

        public AssemblyStepControlViewModel(
            TaskInstanceManager taskManager,
            IParameterStorage parameterStorage,
            ILoggerService loggerService,
            IDialogService dialogService,
            ICompensationService compensationService)
        {
            _taskManager = taskManager;
            _parameterStorage = parameterStorage;
            _logger = loggerService;
            _dialogService = dialogService;
            _assemblyStation = _taskManager.GetTask<AssemblyStation>();
            _compensationService = compensationService;
            // 初始化命令
            StartSingleStepCommand = new DelegateCommand(ExecuteStartSingleStep);
            NextStepCommand = new DelegateCommand(ExecuteNextStep, () => IsStepWaiting);
            StopSingleStepCommand = new DelegateCommand(ExecuteStopSingleStep);
            LoadCompensationCommand = new DelegateCommand(ExecuteLoadCompensation);
            SaveCompensationCommand = new DelegateCommand(ExecuteSaveCompensation);
            ApplyToAllStationsCommand = new DelegateCommand(ExecuteApplyToAllStations);
            ResetCompensationCommand = new DelegateCommand(ExecuteResetCompensation);
            StartAssemblyCommand = new DelegateCommand(ExecuteStartAssembly);
            MoveXRelativeCommand = new DelegateCommand(() => ExecuteRelativeMove("X", CompensationX));
            MoveZRelativeCommand = new DelegateCommand(() => ExecuteRelativeMove("Z", CompensationZ));
            MoveYRelativeCommand = new DelegateCommand(() => ExecuteRelativeMove("Y", CompensationY));
            MoveXTranslateRelativeCommand = new DelegateCommand(() => ExecuteRelativeMove("XTranslate", CompensationXTranslate));
            MoveZPressRelativeCommand = new DelegateCommand(() => ExecuteRelativeMove("ZPress", CompensationZPress));

            // 初始化获取补偿值命令
            GetCompensationXCommand = new DelegateCommand(ExecuteGetCompensationX);
            GetCompensationZCommand = new DelegateCommand(ExecuteGetCompensationZ);
            GetCompensationYCommand = new DelegateCommand(ExecuteGetCompensationY);
            GetCompensationXTranslateCommand = new DelegateCommand(ExecuteGetCompensationXTranslate);
            GetCompensationZPressCommand = new DelegateCommand(ExecuteGetCompensationZPress);

            // 初始化时加载第一个工位的补偿值
            LoadCompensationForStation(1);

            // 设置回调
            if (_assemblyStation != null)
            {
                _assemblyStation.SetStepStatusCallback(UpdateStepStatus);
            }

            AssemblyX = XDevice.Instance.FindAxisById(5);
            AssemblyZ = XDevice.Instance.FindAxisById(0);
            AssemblyY = XDevice.Instance.FindAxisById(9);
            AssemblyU = XDevice.Instance.FindAxisById(1);
        }

        #endregion

        #region 单步控制方法

        private void ExecuteStartSingleStep()
        {
            try
            {
                _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
                {
                    { "title", "单步模式" },
                    { "message", "确认进入单步模式？" }
                }, result =>
                {
                    if (result.Result == ButtonResult.Yes)
                    {
                        try
                        {
                            IsSingleStepMode = true;
                            CurrentStepDescription = "单步模式已启动";

                            // 调用单步启动方法
                            _assemblyStation.StartSingleStepMode();

                            ShowMessage("单步模式已启动", PackIconKind.PlayCircle);
                        }
                        catch (Exception ex)
                        {
                            ShowMessage($"启动单步模式失败: {ex.Message}", PackIconKind.AlertCircle);
                            IsSingleStepMode = false;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                ShowMessage($"启动单步模式异常: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        private void ExecuteNextStep()
        {
            if (!IsStepWaiting) return;

            try
            {
                _assemblyStation.SingleStepNext();
                IsStepWaiting = false;
                NextStepCommand.RaiseCanExecuteChanged();
                //ShowMessage("执行下一步", PackIconKind.SkipNext);
            }
            catch (Exception ex)
            {
                ShowMessage($"执行下一步失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        private void ExecuteStopSingleStep()
        {
            _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
            {
                { "title", "退出单步模式" },
                { "message", "确认退出单步模式？" }
            }, result =>
            {
                if (result.Result == ButtonResult.Yes)
                {
                    try
                    {
                        _assemblyStation.StopSingleStepMode();
                        IsSingleStepMode = false;
                        IsStepWaiting = false;
                        CurrentStepDescription = "单步模式已停止";
                        NextStepCommand.RaiseCanExecuteChanged();
                        ShowMessage("单步模式已停止", PackIconKind.StopCircle);
                    }
                    catch (Exception ex)
                    {
                        ShowMessage($"停止单步模式失败: {ex.Message}", PackIconKind.AlertCircle);
                    }
                }
            });
        }

        // 从父ViewModel更新步骤状态
        public void UpdateStepStatus(string description, bool isWaiting)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                CurrentStepDescription = description;
                IsStepWaiting = isWaiting;
                NextStepCommand.RaiseCanExecuteChanged();
            });
        }

        #endregion

        #region 参数存储辅助方法

        private void LoadCompensationForStation(int stationIndex)
        {
            try
            {
                // 从参数存储加载补偿值
                string identifier = $"AssemblyStation_Station{stationIndex}";
                string _customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                       "Config",
                                       "AssemblyStation");
                CompensationData data = null;

                try
                {
                    // 尝试加载补偿数据
                    data = _parameterStorage.Load<CompensationData>(identifier, _customDirectory);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"加载补偿数据失败（标准目录）: {ex.Message}");

                    // 如果标准目录失败，尝试自定义目录
                    try
                    {
                        string customDirectory = "CompensationParameters";
                        data = _parameterStorage.Load<CompensationData>(identifier, customDirectory);
                    }
                    catch (Exception innerEx)
                    {
                        _logger.Warn($"加载补偿数据失败（自定义目录）: {innerEx.Message}");
                        data = new CompensationData(); // 使用默认值
                    }
                }

                // 更新UI属性
                if (data != null)
                {
                    CompensationX = data.CompensationX;
                    CompensationZ = data.CompensationZ;
                    CompensationY = data.CompensationY;
                    CompensationXTranslate = data.CompensationXTranslate;
                    CompensationZPress = data.CompensationZPress;

                    // 将补偿值更新到补偿服务中
                    UpdateCompensationToService(stationIndex, data);
                }
                else
                {
                    // 使用默认值
                    CompensationX = 0;
                    CompensationZ = 0;
                    CompensationY = 0;
                    CompensationXTranslate = 0;
                    CompensationZPress = 0;

                    // 将默认值更新到补偿服务
                    UpdateCompensationToService(stationIndex, new CompensationData(0, 0, 0, 0, 0, 0));
                }

                CompensationStatus = $"已加载工位{stationIndex}的补偿值";
                _logger.Info($"加载工位{stationIndex}的补偿值: X={CompensationX}, Z={CompensationZ}, Y={CompensationY}, XT={CompensationXTranslate}, ZP={CompensationZPress}");
            }
            catch (Exception ex)
            {
                CompensationStatus = $"加载工位{stationIndex}补偿值失败";
                _logger.Error($"加载补偿值失败: {ex.Message}");
                ShowMessage($"加载补偿值失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        private void SaveCompensationForStation(int stationIndex)
        {
            try
            {
                // 创建补偿数据对象
                var data = new CompensationData(
                    CompensationX,
                    CompensationY,
                    CompensationZ,
                    CompensationXTranslate,
                    CompensationZTranslate,
                    CompensationZPress
                );

                // 保存到参数存储
                string identifier = $"AssemblyStation_Station{stationIndex}";

                try
                {
                    // 尝试保存到标准目录
                    string _customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                       "Config",
                       "AssemblyStation");
                    _parameterStorage.Save(identifier, data, _customDirectory);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"保存补偿数据失败（标准目录）: {ex.Message}");

                    // 如果标准目录失败，尝试自定义目录
                    try
                    {
                        string customDirectory = "CompensationParameters";
                        _parameterStorage.Save(identifier, data, customDirectory);
                    }
                    catch (Exception innerEx)
                    {
                        _logger.Error($"保存补偿数据失败（自定义目录）: {innerEx.Message}");
                        throw; // 重新抛出异常
                    }
                }

                // 将补偿值更新到补偿服务中
                UpdateCompensationToService(stationIndex, data);

                CompensationStatus = $"已保存工位{stationIndex}的补偿值";
                _logger.Info($"保存工位{stationIndex}的补偿值: X={CompensationX}, Z={CompensationZ}, Y={CompensationY}, XT={CompensationXTranslate}, ZP={CompensationZPress}");
                ShowMessage($"工位{stationIndex}的补偿值已保存", PackIconKind.CheckCircle);
            }
            catch (Exception ex)
            {
                CompensationStatus = $"保存工位{stationIndex}补偿值失败";
                _logger.Error($"保存补偿值失败: {ex.Message}");
                ShowMessage($"保存补偿值失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        /// <summary>
        /// 将补偿值更新到补偿服务中
        /// </summary>
        /// <param name="stationIndex">工位索引</param>
        /// <param name="data">补偿数据</param>
        private void UpdateCompensationToService(int stationIndex, CompensationData data)
        {
            try
            {
                // 将界面补偿值映射到补偿服务中的不同补偿类型

                // 1. Tab补偿 - 使用X和Y方向
                var tabCompensation = new Stations.Services.CompensationData
                {
                    CompensationX = data.CompensationX,
                    CompensationY = data.CompensationY,
                    CompensationZ = 0,
                    Source = $"AssemblyStation_Station{stationIndex}",
                };
                _compensationService.UpdateCompensation(stationIndex, CompensationType.Tab, tabCompensation);

                // 2. Slot补偿 - 使用Z方向
                var slotCompensation = new Stations.Services.CompensationData
                {
                    CompensationZ = data.CompensationZ,
                    Source = $"AssemblyStation_Station{stationIndex}",
                };
                _compensationService.UpdateCompensation(stationIndex, CompensationType.Slot, slotCompensation);

                // 3. Actuator补偿 - 使用X平移
                var actuatorCompensation = new Stations.Services.CompensationData
                {
                    CompensationXTranslate = data.CompensationXTranslate,
                    Source = $"AssemblyStation_Station{stationIndex}",
                };
                _compensationService.UpdateCompensation(stationIndex, CompensationType.ActuatorX, actuatorCompensation);

                // 4. TabZ补偿 - 使用Z下压
                var tabZCompensation = new Stations.Services.CompensationData
                {
                    CompensationZPress = data.CompensationZPress,
                    Source = $"AssemblyStation_Station{stationIndex}",
                };
                _compensationService.UpdateCompensation(stationIndex, CompensationType.PressZ, tabZCompensation);

                _logger.Debug($"已将工位{stationIndex}补偿值更新到补偿服务");
            }
            catch (Exception ex)
            {
                _logger.Error($"更新补偿服务失败（工位{stationIndex}）: {ex.Message}");
                // 不抛出异常，不影响主流程
            }
        }

        /// <summary>
        /// 从补偿服务读取补偿值到界面
        /// </summary>
        /// <param name="stationIndex">工位索引</param>
        private void LoadCompensationFromService(int stationIndex)
        {
            try
            {
                // 从补偿服务读取不同类型补偿
                var tabComp = _compensationService.GetCompensation(stationIndex, CompensationType.Tab);
                var slotComp = _compensationService.GetCompensation(stationIndex, CompensationType.Slot);
                var actuatorComp = _compensationService.GetCompensation(stationIndex, CompensationType.ActuatorX);
                var tabZComp = _compensationService.GetCompensation(stationIndex, CompensationType.PressZ);

                // 映射到界面属性
                CompensationX = tabComp.CompensationX;
                CompensationY = tabComp.CompensationY;
                CompensationZ = slotComp.CompensationZ;
                CompensationXTranslate = actuatorComp.CompensationX;
                CompensationZPress = tabZComp.CompensationZPress;

                _logger.Debug($"从补偿服务读取工位{stationIndex}补偿值成功");
            }
            catch (Exception ex)
            {
                _logger.Warn($"从补偿服务读取工位{stationIndex}补偿值失败: {ex.Message}");
                // 使用默认值
                CompensationX = 0;
                CompensationZ = 0;
                CompensationY = 0;
                CompensationXTranslate = 0;
                CompensationZPress = 0;
            }
        }

        #endregion

        #region 补偿值管理方法

        private void ExecuteLoadCompensation()
        {
            // 可以选择从哪个源加载：配置文件或补偿服务
            _dialogService.ShowDialog("SelectionDialog", new DialogParameters
            {
                { "title", "加载补偿值" },
                { "message", "请选择补偿值来源：" },
                { "options", new List<string> { "从配置文件加载", "从补偿服务加载", "同时从两者加载" } }
            }, result =>
            {
                if (result.Result == ButtonResult.OK && result.Parameters.ContainsKey("selectedOption"))
                {
                    string option = result.Parameters.GetValue<string>("selectedOption");

                    switch (option)
                    {
                        case "从配置文件加载":
                            LoadCompensationForStation(SelectedStationIndex);
                            break;

                        case "从补偿服务加载":
                            LoadCompensationFromService(SelectedStationIndex);
                            CompensationStatus = $"已从补偿服务加载工位{SelectedStationIndex}的补偿值";
                            break;

                        case "同时从两者加载":
                            // 先加载配置文件，然后检查差异
                            var oldX = CompensationX;
                            var oldZ = CompensationZ;
                            var oldY = CompensationY;
                            var oldXT = CompensationXTranslate;
                            var oldZP = CompensationZPress;

                            LoadCompensationForStation(SelectedStationIndex);
                            LoadCompensationFromService(SelectedStationIndex);

                            // 检查是否有差异
                            bool hasDifference =
                                Math.Abs(oldX - CompensationX) > 0.001 ||
                                Math.Abs(oldZ - CompensationZ) > 0.001 ||
                                Math.Abs(oldY - CompensationY) > 0.001 ||
                                Math.Abs(oldXT - CompensationXTranslate) > 0.001 ||
                                Math.Abs(oldZP - CompensationZPress) > 0.001;

                            if (hasDifference)
                            {
                                CompensationStatus = $"已从两个源加载，值可能不同，请确认";
                                ShowMessage("配置文件和补偿服务中的值不同，请确认使用哪个值", PackIconKind.Warning);
                            }
                            else
                            {
                                CompensationStatus = $"已从两个源加载工位{SelectedStationIndex}的补偿值";
                            }
                            break;
                    }
                }
            });
        }

        private void ExecuteSaveCompensation()
        {
            SaveCompensationForStation(SelectedStationIndex);
        }

        private void ExecuteApplyToAllStations()
        {
            _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
            {
                { "title", "应用所有工位" },
                { "message", $"确认将当前补偿值应用到所有6个工位？\nX={CompensationX}, Z={CompensationZ}, Y={CompensationY}, XT={CompensationXTranslate}, ZP={CompensationZPress}" }
            }, result =>
            {
                if (result.Result == ButtonResult.Yes)
                {
                    try
                    {
                        for (int i = 1; i <= 6; i++)
                        {
                            // 创建补偿数据对象
                            var data = new CompensationData(
                                CompensationX,
                                CompensationY,
                                CompensationZ,
                                CompensationXTranslate,
                                CompensationZTranslate,
                                CompensationZPress
                            );

                            // 保存到参数存储
                            string identifier = $"AssemblyStation_Station{i}";

                            try
                            {
                                string _customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                       "Config",
                                                       "AssemblyStation");
                                _parameterStorage.Save(identifier, data, _customDirectory);
                            }
                            catch
                            {
                                // 如果标准目录失败，尝试自定义目录
                                string customDirectory = "CompensationParameters";
                                _parameterStorage.Save(identifier, data, customDirectory);
                            }

                            // 同时更新到补偿服务
                            UpdateCompensationToService(i, data);
                        }

                        CompensationStatus = "已将所有补偿值应用到所有工位";
                        _logger.Info($"应用所有工位补偿值: X={CompensationX}, Z={CompensationZ}, Y={CompensationY}, XT={CompensationXTranslate}, ZP={CompensationZPress}");
                        ShowMessage("补偿值已应用到所有工位", PackIconKind.CheckCircle);
                    }
                    catch (Exception ex)
                    {
                        CompensationStatus = "应用补偿值失败";
                        _logger.Error($"应用所有工位补偿值失败: {ex.Message}");
                        ShowMessage($"应用补偿值失败: {ex.Message}", PackIconKind.AlertCircle);
                    }
                }
            });
        }

        private void ExecuteResetCompensation()
        {
            _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
            {
                { "title", "重置补偿值" },
                { "message", "确认重置当前工位的所有补偿值为0？" }
            }, result =>
            {
                if (result.Result == ButtonResult.Yes)
                {
                    CompensationX = 0;
                    CompensationZ = 0;
                    CompensationY = 0;
                    CompensationXTranslate = 0;
                    CompensationZPress = 0;

                    // 更新到补偿服务
                    UpdateCompensationToService(SelectedStationIndex, new CompensationData(0, 0, 0, 0, 0, 0));

                    CompensationStatus = "已重置当前工位补偿值";
                    ShowMessage("补偿值已重置", PackIconKind.Refresh);
                }
            });
        }

        #endregion

        #region 获取补偿值命令实现

        /// <summary>
        /// 从补偿服务获取X轴补偿值
        /// </summary>
        private void ExecuteGetCompensationX()
        {
            try
            {
                // 从补偿服务获取Tab补偿（包含X和Y方向）
                var tabCompensationX1 = _compensationService.GetCompensation(SelectedStationIndex, CompensationType.Tab);
                var tabCompensationX2 = _compensationService.GetCompensation(1, CompensationType.Slot);
                if (tabCompensationX1 != null)
                {
                    CompensationX = tabCompensationX1.CompensationX + tabCompensationX2.CompensationX;//Tab补偿的X方向和Slot补偿的X方向加起来
                    CompensationStatus = $"已获取工位{SelectedStationIndex}的X轴补偿值: {CompensationX:F3} mm";
                    _logger.Info($"从补偿服务获取工位{SelectedStationIndex}的X轴补偿值: {CompensationX:F3} mm");
                    ShowMessage($"已获取X轴补偿值: {CompensationX:F3} mm", PackIconKind.CheckCircle);
                }
                else
                {
                    CompensationStatus = "未找到X轴补偿数据";
                    ShowMessage("未找到X轴补偿数据", PackIconKind.Alert);
                }
            }
            catch (Exception ex)
            {
                CompensationStatus = $"获取X轴补偿值失败";
                _logger.Error($"获取X轴补偿值失败: {ex.Message}");
                ShowMessage($"获取X轴补偿值失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        /// <summary>
        /// 从补偿服务获取Z轴补偿值
        /// </summary>
        private void ExecuteGetCompensationZ()
        {
            try
            {
                // 从补偿服务获取Slot补偿（Z方向）
                var slotCompensation = _compensationService.GetCompensation(SelectedStationIndex, CompensationType.Slot);
                if (slotCompensation != null)
                {
                    CompensationZ = slotCompensation.CompensationZ;
                    CompensationStatus = $"已获取工位{SelectedStationIndex}的Z轴补偿值: {CompensationZ:F3} mm";
                    _logger.Info($"从补偿服务获取工位{SelectedStationIndex}的Z轴补偿值: {CompensationZ:F3} mm");
                    ShowMessage($"已获取Z轴补偿值: {CompensationZ:F3} mm", PackIconKind.CheckCircle);
                }
                else
                {
                    CompensationStatus = "未找到Z轴补偿数据";
                    ShowMessage("未找到Z轴补偿数据", PackIconKind.Alert);
                }
            }
            catch (Exception ex)
            {
                CompensationStatus = $"获取Z轴补偿值失败";
                _logger.Error($"获取Z轴补偿值失败: {ex.Message}");
                ShowMessage($"获取Z轴补偿值失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        /// <summary>
        /// 从补偿服务获取Y轴补偿值
        /// </summary>
        private void ExecuteGetCompensationY()
        {
            try
            {
                // 从补偿服务获取Tab补偿（包含X和Y方向）
                var tabCompensation = _compensationService.GetCompensation(SelectedStationIndex, CompensationType.Tab);
                if (tabCompensation != null)
                {
                    CompensationY = tabCompensation.CompensationY;
                    CompensationStatus = $"已获取工位{SelectedStationIndex}的Y轴补偿值: {CompensationY:F3} mm";
                    _logger.Info($"从补偿服务获取工位{SelectedStationIndex}的Y轴补偿值: {CompensationY:F3} mm");
                    ShowMessage($"已获取Y轴补偿值: {CompensationY:F3} mm", PackIconKind.CheckCircle);
                }
                else
                {
                    CompensationStatus = "未找到Y轴补偿数据";
                    ShowMessage("未找到Y轴补偿数据", PackIconKind.Alert);
                }
            }
            catch (Exception ex)
            {
                CompensationStatus = $"获取Y轴补偿值失败";
                _logger.Error($"获取Y轴补偿值失败: {ex.Message}");
                ShowMessage($"获取Y轴补偿值失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        /// <summary>
        /// 从补偿服务获取X平移轴补偿值
        /// </summary>
        private void ExecuteGetCompensationXTranslate()
        {
            try
            {
                // 从补偿服务获取Actuator补偿（X平移方向）
                var actuatorCompensation = _compensationService.GetCompensation(SelectedStationIndex, CompensationType.ActuatorX);
                if (actuatorCompensation != null)
                {
                    CompensationXTranslate = actuatorCompensation.CompensationX;
                    CompensationStatus = $"已获取工位{SelectedStationIndex}的X平移补偿值: {CompensationXTranslate:F3} mm";
                    _logger.Info($"从补偿服务获取工位{SelectedStationIndex}的X平移补偿值: {CompensationXTranslate:F3} mm");
                    ShowMessage($"已获取X平移补偿值: {CompensationXTranslate:F3} mm", PackIconKind.CheckCircle);
                }
                else
                {
                    CompensationStatus = "未找到X平移补偿数据";
                    ShowMessage("未找到X平移补偿数据", PackIconKind.Alert);
                }
            }
            catch (Exception ex)
            {
                CompensationStatus = $"获取X平移补偿值失败";
                _logger.Error($"获取X平移补偿值失败: {ex.Message}");
                ShowMessage($"获取X平移补偿值失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        /// <summary>
        /// 从补偿服务获取Z下压轴补偿值
        /// </summary>
        private void ExecuteGetCompensationZPress()
        {
            try
            {
                // 从补偿服务获取PressZ补偿（Z下压方向）
                var pressZCompensation = _compensationService.GetCompensation(SelectedStationIndex, CompensationType.PressZ);
                if (pressZCompensation != null)
                {
                    CompensationZPress = pressZCompensation.CompensationZ;
                    CompensationStatus = $"已获取工位{SelectedStationIndex}的Z下压补偿值: {CompensationZPress:F3} mm";
                    _logger.Info($"从补偿服务获取工位{SelectedStationIndex}的Z下压补偿值: {CompensationZPress:F3} mm");
                    ShowMessage($"已获取Z下压补偿值: {CompensationZPress:F3} mm", PackIconKind.CheckCircle);
                }
                else
                {
                    CompensationStatus = "未找到Z下压补偿数据";
                    ShowMessage("未找到Z下压补偿数据", PackIconKind.Alert);
                }
            }
            catch (Exception ex)
            {
                CompensationStatus = $"获取Z下压补偿值失败";
                _logger.Error($"获取Z下压补偿值失败: {ex.Message}");
                ShowMessage($"获取Z下压补偿值失败: {ex.Message}", PackIconKind.AlertCircle);
            }
        }

        #endregion

        #region 组装执行方法

        private async void ExecuteStartAssembly()
        {
            try
            {
                IsAssemblyRunning = true;
                AssemblyStatus = $"开始工位{SelectedStationIndex}的组装...";

                // 首先保存当前补偿值
                //SaveCompensationForStation(SelectedStationIndex);
                // 组装前从补偿服务读取最新补偿值（确保使用最新值）
                var tabComp = _compensationService.GetCompensation(SelectedStationIndex, CompensationType.Tab);
                var slotComp = _compensationService.GetCompensation(SelectedStationIndex, CompensationType.Slot);
                var actuatorComp = _compensationService.GetCompensation(SelectedStationIndex, CompensationType.ActuatorX);
                var tabZComp = _compensationService.GetCompensation(SelectedStationIndex, CompensationType.PressZ);

                _logger.Info($"组装使用补偿值 - 工位{SelectedStationIndex}: " +
                           $"Tab(X={tabComp.CompensationX}, Y={tabComp.CompensationY}), " +
                           $"Slot(Z={slotComp.CompensationZ}), " +
                           $"Actuator(X={actuatorComp.CompensationX}), " +
                           $"TabZ(Z={tabZComp.CompensationZ})");

                // 调用组装方法
                await _assemblyStation.AssembleModule(SelectedStationIndex);

                AssemblyStatus = $"工位{SelectedStationIndex}组装完成";
            }
            catch (Exception ex)
            {
                AssemblyStatus = $"工位{SelectedStationIndex}组装异常";
                ShowMessage($"组装异常: {ex.Message}", PackIconKind.AlertCircle);
                _logger.Error($"工位{SelectedStationIndex}组装异常: {ex.Message}");
            }
            finally
            {
                IsAssemblyRunning = false;
            }
        }

        #endregion

        #region 辅助方法
        // 相对移动执行方法
        private void ExecuteRelativeMove(string axis, double distance)
        {
            if (Math.Abs(distance) < 0.001)
            {
                ShowMessage($"补偿值为0，无需移动{axis}轴", PackIconKind.Information);
                return;
            }

            _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
            {
                { "title", "相对移动确认" },
                { "message", $"确认将{axis}轴相对移动 {distance:F3} mm 吗？" }
            }, result =>
            {
                if (result.Result == ButtonResult.Yes)
                {
                    try
                    {
                        // 调用轴移动服务
                        bool success = false;
                        string axisName = "";

                        switch (axis)
                        {
                            case "X":
                                success = _assemblyStation.MoveAxisRelative(AssemblyX, distance);
                                break;
                            case "Z":
                                success = _assemblyStation.MoveAxisRelative(AssemblyZ, distance);
                                break;
                            case "Y":
                                success = _assemblyStation.MoveAxisRelative(AssemblyY, distance);
                                break;
                            case "XTranslate":
                                success = _assemblyStation.MoveAxisRelative(AssemblyX, distance);
                                break;
                            case "ZPress":
                                success = _assemblyStation.MoveAxisRelative(AssemblyZ, distance);
                                break;
                        }

                        if (success)
                        {
                            ShowMessage($"{axisName}轴已相对移动 {distance:F3} mm", PackIconKind.CheckCircle);
                            _logger.Info($"{axisName}轴相对移动 {distance:F3} mm 完成");
                        }
                        else
                        {
                            ShowMessage($"{axisName}轴移动失败", PackIconKind.AlertCircle);
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowMessage($"轴移动异常: {ex.Message}", PackIconKind.AlertCircle);
                        _logger.Error($"执行相对移动失败（轴={axis}, 距离={distance}）: {ex.Message}");
                    }
                }
            });
        }
        private void ShowMessage(string message, PackIconKind iconKind = PackIconKind.AlertCircle)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                {
                    { "title", "提示" },
                    { "message", message },
                    { "icon", iconKind }
                }, result =>
                {
                    // 用户点击确认后的逻辑
                });
            });
        }

        #endregion
    }
}