using Core.Abstraction;
using Core.Constants;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Module.Services;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Mvvm;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Module.ViewModels
{
    /// <summary>
    /// 针头校准验证：四点寻针实测 + 硬件已生效 TCP 总补偿，与固定基准 ReferenceXYZ 对比偏差
    /// </summary>
    public class NeedleCalibrationVerifyViewModel : BindableBase
    {
        private readonly INeedleAlignerMotionService _needleMotion;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        private readonly IRecipePoolService _recipePoolService;

        /// <summary>验证记录文件保留天数</summary>
        private const int ConfigRetentionDays = 30;

        private CancellationTokenSource _verificationCts;

        private int _selectedSystemNumber = 1;
        /// <summary>当前选择的系统编号（1或2）</summary>
        public int SelectedSystemNumber
        {
            get => _selectedSystemNumber;
            set
            {
                if (SetProperty(ref _selectedSystemNumber, value))
                {
                    _ = TryAutoLoadConfigAsync();
                    _ = RefreshCalibrationReferenceAsync();
                }
            }
        }

        private bool _isVerifying;
        public bool IsVerifying
        {
            get => _isVerifying;
            set
            {
                if (SetProperty(ref _isVerifying, value))
                    RaisePropertyChanged(nameof(CanVerify));
            }
        }

        private double _verificationProgress;
        public double VerificationProgress
        {
            get => _verificationProgress;
            set => SetProperty(ref _verificationProgress, value);
        }

        private string _verificationStatus = "Ready";
        public string VerificationStatus
        {
            get => _verificationStatus;
            set => SetProperty(ref _verificationStatus, value);
        }

        private double _measuredX;
        /// <summary>本次四点寻针实测 X</summary>
        public double MeasuredX
        {
            get => _measuredX;
            set => SetProperty(ref _measuredX, value);
        }

        private double _measuredY;
        public double MeasuredY
        {
            get => _measuredY;
            set => SetProperty(ref _measuredY, value);
        }

        private double _measuredZ;
        public double MeasuredZ
        {
            get => _measuredZ;
            set => SetProperty(ref _measuredZ, value);
        }

        private double _activeTcpX;
        /// <summary>当前硬件已生效 TCP 总补偿 X（来自全局变量）</summary>
        public double ActiveTcpX
        {
            get => _activeTcpX;
            set => SetProperty(ref _activeTcpX, value);
        }

        private double _activeTcpY;
        public double ActiveTcpY
        {
            get => _activeTcpY;
            set => SetProperty(ref _activeTcpY, value);
        }

        private double _activeTcpZ;
        public double ActiveTcpZ
        {
            get => _activeTcpZ;
            set => SetProperty(ref _activeTcpZ, value);
        }

        private double _effectiveX;
        /// <summary>有效坐标 X = 实测 + 已生效 TCP</summary>
        public double EffectiveX
        {
            get => _effectiveX;
            set => SetProperty(ref _effectiveX, value);
        }

        private double _effectiveY;
        public double EffectiveY
        {
            get => _effectiveY;
            set => SetProperty(ref _effectiveY, value);
        }

        private double _effectiveZ;
        public double EffectiveZ
        {
            get => _effectiveZ;
            set => SetProperty(ref _effectiveZ, value);
        }

        private double _referenceX;
        /// <summary>固定示教基准 ReferenceXYZ.X</summary>
        public double ReferenceX
        {
            get => _referenceX;
            set => SetProperty(ref _referenceX, value);
        }

        private double _referenceY;
        public double ReferenceY
        {
            get => _referenceY;
            set => SetProperty(ref _referenceY, value);
        }

        private double _referenceZ;
        public double ReferenceZ
        {
            get => _referenceZ;
            set => SetProperty(ref _referenceZ, value);
        }

        private double _deviationX;
        /// <summary>偏差 |Reference - Effective|</summary>
        public double DeviationX
        {
            get => _deviationX;
            set => SetProperty(ref _deviationX, value);
        }

        private double _deviationY;
        public double DeviationY
        {
            get => _deviationY;
            set => SetProperty(ref _deviationY, value);
        }

        private double _deviationZ;
        public double DeviationZ
        {
            get => _deviationZ;
            set => SetProperty(ref _deviationZ, value);
        }

        private string _resultX = "-";
        public string ResultX
        {
            get => _resultX;
            set => SetProperty(ref _resultX, value);
        }

        private string _resultY = "-";
        public string ResultY
        {
            get => _resultY;
            set => SetProperty(ref _resultY, value);
        }

        private string _resultZ = "-";
        public string ResultZ
        {
            get => _resultZ;
            set => SetProperty(ref _resultZ, value);
        }

        private Brush _resultXColor = Brushes.Gray;
        public Brush ResultXColor
        {
            get => _resultXColor;
            set => SetProperty(ref _resultXColor, value);
        }

        private Brush _resultYColor = Brushes.Gray;
        public Brush ResultYColor
        {
            get => _resultYColor;
            set => SetProperty(ref _resultYColor, value);
        }

        private Brush _resultZColor = Brushes.Gray;
        public Brush ResultZColor
        {
            get => _resultZColor;
            set => SetProperty(ref _resultZColor, value);
        }

        private string _overallResult = "-";
        public string OverallResult
        {
            get => _overallResult;
            set => SetProperty(ref _overallResult, value);
        }

        private Brush _overallResultColor = Brushes.Gray;
        public Brush OverallResultColor
        {
            get => _overallResultColor;
            set => SetProperty(ref _overallResultColor, value);
        }

        private string _currentFilePath;
        /// <summary>当前验证记录文件完整路径</summary>
        public string CurrentFilePath
        {
            get => _currentFilePath;
            set => SetProperty(ref _currentFilePath, value);
        }

        private string _currentFileName;
        /// <summary>当前验证记录文件名（显示用）</summary>
        public string CurrentFileName
        {
            get => _currentFileName;
            set => SetProperty(ref _currentFileName, value);
        }

        public ObservableCollection<string> VerificationLogs { get; } = new ObservableCollection<string>();

        public bool CanVerify => !IsVerifying;

        public DelegateCommand ExecuteVerificationCommand { get; }
        public DelegateCommand CancelVerificationCommand { get; }
        public DelegateCommand SaveReportCommand { get; }
        public DelegateCommand LoadParametersCommand { get; }
        public DelegateCommand ClearLogCommand { get; }
        public DelegateCommand System1Command { get; }
        public DelegateCommand System2Command { get; }

        private string _lastReportSummary;
        public string LastReportSummary
        {
            get => _lastReportSummary;
            set => SetProperty(ref _lastReportSummary, value);
        }

        public NeedleCalibrationVerifyViewModel(
            INeedleAlignerMotionService needleMotion,
            ILoggerService logger,
            ILocalizationService localization,
            IRecipePoolService recipePoolService)
        {
            _needleMotion = needleMotion;
            _logger = logger;
            _localization = localization;
            _recipePoolService = recipePoolService;

            ExecuteVerificationCommand = new DelegateCommand(async () => await ExecuteVerificationAsync(), () => CanVerify)
                .ObservesCanExecute(() => CanVerify);
            CancelVerificationCommand = new DelegateCommand(CancelVerification, () => IsVerifying)
                .ObservesCanExecute(() => IsVerifying);
            SaveReportCommand = new DelegateCommand(async () => await SaveVerificationRecordAsync());
            LoadParametersCommand = new DelegateCommand(async () => await LoadVerificationRecordAsync());
            ClearLogCommand = new DelegateCommand(ClearLog);
            System1Command = new DelegateCommand(() => SelectedSystemNumber = 1);
            System2Command = new DelegateCommand(() => SelectedSystemNumber = 2);

            _ = InitializeAsync();
        }

        /// <summary>初始化：加载基准参考值与最近验证记录</summary>
        private async Task InitializeAsync()
        {
            await RefreshCalibrationReferenceAsync();
            await TryAutoLoadConfigAsync();
        }

        /// <summary>
        /// 执行验证：INeedleAlignerMotionService 四点寻针 → 读取硬件 TCP → 与 ReferenceXYZ 对比
        /// 公式：Deviation = |ReferenceXYZ - (Measured + ActiveTcp)|
        /// </summary>
        private async Task ExecuteVerificationAsync()
        {
            IsVerifying = true;
            VerificationProgress = 0;
            VerificationStatus = L("NeedleVerify_Status_Preparing", "准备验证...");
            ResetResults();

            _verificationCts?.Dispose();
            _verificationCts = new CancellationTokenSource();
            var token = _verificationCts.Token;

            var verificationSucceeded = false;
            try
            {
                AddLog(L("NeedleVerify_Log_Start", "开始验证 - 系统 {0}"), SelectedSystemNumber.ToString());

                VerificationStatus = L("NeedleVerify_Status_LoadingParams", "加载校准参数...");
                var parameters = await LoadLatestCalibrationParamsAsync();
                if (parameters?.ReferenceXYZ == null)
                {
                    VerificationStatus = L("NeedleVerify_Status_NoCalibrationConfig", "未找到校准配置");
                    AddLog(L("NeedleVerify_Log_NoCalibrationConfig", "未找到系统 {0} 的校准 JSON，请先在 NeedleTcp 页完成配置"), SelectedSystemNumber.ToString());
                    return;
                }

                ReferenceX = parameters.ReferenceXYZ.X;
                ReferenceY = parameters.ReferenceXYZ.Y;
                ReferenceZ = parameters.ReferenceXYZ.Z;

                VerificationProgress = 5;
                VerificationStatus = L("NeedleVerify_Status_ReadingActiveTcp", "读取硬件已生效 TCP...");
                var activeTcp = await ReadActiveTcpCompensationAsync(parameters);
                ActiveTcpX = activeTcp.x;
                ActiveTcpY = activeTcp.y;
                ActiveTcpZ = activeTcp.z;
                AddLog(L("NeedleVerify_Log_ActiveTcp", "硬件已生效 TCP: X={0:F4}, Y={1:F4}, Z={2:F4}"),
                    ActiveTcpX.ToString("F4"), ActiveTcpY.ToString("F4"), ActiveTcpZ.ToString("F4"));
                AddLog(L("NeedleVerify_Log_Reference", "固定基准 Reference: X={0:F4}, Y={1:F4}, Z={2:F4}"),
                    ReferenceX.ToString("F4"), ReferenceY.ToString("F4"), ReferenceZ.ToString("F4"));

                var parametersSnapshot = parameters.Clone();
                var systemNumber = SelectedSystemNumber;

                var progress = new Progress<NeedleAlignerProgressReport>(p =>
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        if (!string.IsNullOrEmpty(p.Status))
                            VerificationStatus = p.Status;
                        VerificationProgress = Math.Min(85, 10 + p.Progress * 0.75);
                        if (!string.IsNullOrEmpty(p.DetailLog))
                            AddLog(p.DetailLog);
                    });
                });

                VerificationStatus = L("NeedleVerify_Status_EdgeSearch", "四点寻边测量...");
                AddLog(L("NeedleVerify_Log_EdgeSearch", "执行四点寻边与 Z 接触测量（与 NeedleTcp 校准流程一致）..."));

                var result = await Task.Run(async () =>
                    await _needleMotion.ExecuteNeedleCalibrationAsync(
                        parametersSnapshot, systemNumber, progress, token), token);

                if (!result.Success)
                {
                    VerificationStatus = L("NeedleVerify_Status_Error", "验证异常");
                    AddLog(L("NeedleVerify_Log_MeasureFailed", "寻针测量失败: {0}"), result.ErrorMessage ?? "-");
                    return;
                }

                MeasuredX = result.MeasuredCenter.X;
                MeasuredY = result.MeasuredCenter.Y;
                MeasuredZ = result.MeasuredHeight;

                AddLog(L("NeedleVerify_Log_Measured", "实测坐标: X={0:F4}, Y={1:F4}, Z={2:F4}"),
                    MeasuredX.ToString("F4"), MeasuredY.ToString("F4"), MeasuredZ.ToString("F4"));

                // 有效坐标 = 实测 + 硬件已生效总补偿
                EffectiveX = MeasuredX + ActiveTcpX;
                EffectiveY = MeasuredY + ActiveTcpY;
                EffectiveZ = MeasuredZ + ActiveTcpZ;

                VerificationStatus = L("NeedleVerify_Status_Comparing", "与固定基准对比...");
                AddLog(L("NeedleVerify_Log_Effective", "有效坐标 (实测+TCP): X={0:F4}, Y={1:F4}, Z={2:F4}"),
                    EffectiveX.ToString("F4"), EffectiveY.ToString("F4"), EffectiveZ.ToString("F4"));

                // 偏差 = |ReferenceXYZ - Effective|
                DeviationX = Math.Abs(ReferenceX - EffectiveX);
                DeviationY = Math.Abs(ReferenceY - EffectiveY);
                DeviationZ = Math.Abs(ReferenceZ - EffectiveZ);

                VerificationProgress = 90;

                var (xResult, xColor) = EvaluateDeviation(DeviationX);
                var (yResult, yColor) = EvaluateDeviation(DeviationY);
                var (zResult, zColor) = EvaluateDeviation(DeviationZ);

                ResultX = xResult;
                ResultY = yResult;
                ResultZ = zResult;
                ResultXColor = xColor;
                ResultYColor = yColor;
                ResultZColor = zColor;

                var results = new[] { (xResult, xColor, DeviationX), (yResult, yColor, DeviationY), (zResult, zColor, DeviationZ) };
                var worst = results.OrderByDescending(r => r.Item3).First();
                OverallResult = worst.Item1;
                OverallResultColor = worst.Item2;

                VerificationProgress = 100;
                VerificationStatus = L("NeedleVerify_Status_Completed", "验证完成");

                AddLog(L("NeedleVerify_Log_Completed",
                    "验证完成 - 偏差 X:{0:F4}({1}) Y:{2:F4}({3}) Z:{4:F4}({5}) 综合:{6}"),
                    DeviationX.ToString("F4"), ResultX,
                    DeviationY.ToString("F4"), ResultY,
                    DeviationZ.ToString("F4"), ResultZ,
                    OverallResult);

                _logger?.Info($"[NeedleVerify] 系统{SelectedSystemNumber}验证完成: " +
                              $"Measured=({MeasuredX:F4},{MeasuredY:F4},{MeasuredZ:F4}) " +
                              $"ActiveTcp=({ActiveTcpX:F4},{ActiveTcpY:F4},{ActiveTcpZ:F4}) " +
                              $"Effective=({EffectiveX:F4},{EffectiveY:F4},{EffectiveZ:F4}) " +
                              $"Ref=({ReferenceX:F4},{ReferenceY:F4},{ReferenceZ:F4}) " +
                              $"Dev=({DeviationX:F4},{DeviationY:F4},{DeviationZ:F4}) Overall={OverallResult}");

                verificationSucceeded = true;
            }
            catch (OperationCanceledException)
            {
                VerificationStatus = L("NeedleVerify_Status_Cancelled", "验证已取消");
                AddLog(L("NeedleVerify_Log_Cancelled", "验证已取消"));
            }
            catch (Exception ex)
            {
                VerificationStatus = L("NeedleVerify_Status_Error", "验证异常");
                AddLog(L("NeedleVerify_Log_Error", "验证异常: {0}"), ex.Message);
                _logger?.Error($"[NeedleVerify] 验证失败: {ex.Message}");
            }
            finally
            {
                IsVerifying = false;
                _verificationCts?.Dispose();
                _verificationCts = null;
            }

            if (verificationSucceeded)
                await SaveVerificationRecordAsync();
        }

        /// <summary>
        /// 取消验证：先触发 CancellationToken 通知异步流程退出，再显式停止运动轴，双重保险。
        /// 工业控制安全要求：取消时必须立即停止物理运动，避免运动残留。
        /// </summary>
        private void CancelVerification()
        {
            try
            {
                AddLog(L("NeedleVerify_Log_CancelRequest", "用户请求取消验证..."));
                _verificationCts?.Cancel();

                // 显式停止运动轴，防止 token 传播延迟期间运动继续
                _needleMotion.StopMotion(SelectedSystemNumber);
                _logger?.Info($"[NeedleVerify] 系统{SelectedSystemNumber} 验证已取消，运动轴已停止");
            }
            catch (Exception ex)
            {
                _logger?.Error($"[NeedleVerify] 取消验证异常: {ex.Message}");
            }
        }

        /// <summary>从 NeedleTcp 同款路径加载最新校准 JSON</summary>
        private async Task<NeedleCalibrationParams> LoadLatestCalibrationParamsAsync()
        {
            try
            {
                var poolName = _recipePoolService?.CurrentPoolName ?? "Default";
                var extKey = $"NeedleAligner_CurrentFile_System{SelectedSystemNumber}";
                var extData = await _recipePoolService.GetExtensionDataAsync<NeedleAlignerFileRecord>(poolName, extKey);

                if (extData?.FilePath != null && File.Exists(extData.FilePath))
                    return await LoadCalibrationParamsFromFileAsync(extData.FilePath);

                var calibrationDir = GetCalibrationDirectory(SelectedSystemNumber);
                var latest = Directory
                    .EnumerateFiles(calibrationDir, $"NeedleCalibration_System{SelectedSystemNumber}_*.json")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();

                if (latest != null)
                    return await LoadCalibrationParamsFromFileAsync(latest);
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[NeedleVerify] 加载校准参数失败: {ex.Message}");
            }

            return null;
        }

        private static async Task<NeedleCalibrationParams> LoadCalibrationParamsFromFileAsync(string filePath)
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonConvert.DeserializeObject<NeedleCalibrationParams>(json);
        }

        /// <summary>切换系统时刷新界面上的固定基准显示</summary>
        private async Task RefreshCalibrationReferenceAsync()
        {
            var parameters = await LoadLatestCalibrationParamsAsync();
            if (parameters?.ReferenceXYZ == null) return;

            ReferenceX = parameters.ReferenceXYZ.X;
            ReferenceY = parameters.ReferenceXYZ.Y;
            ReferenceZ = parameters.ReferenceXYZ.Z;

            var activeTcp = await ReadActiveTcpCompensationAsync(parameters);
            ActiveTcpX = activeTcp.x;
            ActiveTcpY = activeTcp.y;
            ActiveTcpZ = activeTcp.z;
        }

        /// <summary>从配方池全局变量读取当前硬件已生效 TCP 总补偿</summary>
        private async Task<(double x, double y, double z)> ReadActiveTcpCompensationAsync(NeedleCalibrationParams parameters)
        {
            if (_recipePoolService == null || parameters == null)
                return (0, 0, 0);

            var poolId = _recipePoolService.CurrentPoolName ?? "Default";
            var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);

            var xName = ResolveLinkedVarName(parameters.CompensationXLinkedVar, NeedleAlignerGlobalVariableNames.DefaultCompXLinkedVar);
            var yName = ResolveLinkedVarName(parameters.CompensationYLinkedVar, NeedleAlignerGlobalVariableNames.DefaultCompYLinkedVar);
            var zName = ResolveLinkedVarName(parameters.CompensationZLinkedVar, NeedleAlignerGlobalVariableNames.DefaultCompZLinkedVar);

            return (
                GetDoubleGlobalVariableValue(variables, xName),
                GetDoubleGlobalVariableValue(variables, yName),
                GetDoubleGlobalVariableValue(variables, zName));
        }

        private static string ResolveLinkedVarName(string linkedVar, string defaultName)
            => string.IsNullOrWhiteSpace(linkedVar) ? defaultName : linkedVar;

        private static double GetDoubleGlobalVariableValue(IEnumerable<GlobalVariable> variables, string name)
        {
            var variable = variables?.FirstOrDefault(v =>
                string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
            if (variable == null) return 0;

            return double.TryParse(variable.Value, out var value) ? value : 0;
        }

        /// <summary>保存验证记录到默认目录，文件名带时间戳，并清理过期文件</summary>
        private async Task SaveVerificationRecordAsync()
        {
            try
            {
                LastReportSummary = BuildReportSummary(DateTime.Now);
                var configDir = GetVerificationDirectory(SelectedSystemNumber);
                var fileName = $"NeedleVerify_System{SelectedSystemNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(configDir, fileName);

                var record = BuildCurrentRecord();
                var json = JsonConvert.SerializeObject(record, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, json);

                CurrentFilePath = filePath;
                CurrentFileName = fileName;
                await SaveCurrentFileToRecipePoolAsync();
                QueueCleanupOldConfigFiles(configDir, filePath, SelectedSystemNumber);

                AddLog(L("NeedleVerify_Log_ReportSaved", "报告已保存: {0}"), fileName);
                _logger?.Info($"[NeedleVerify] 系统{SelectedSystemNumber}验证记录已保存: {filePath}");
            }
            catch (Exception ex)
            {
                AddLog(L("NeedleVerify_Log_ReportError", "报告保存失败: {0}"), ex.Message);
                _logger?.Error($"[NeedleVerify] 验证记录保存失败: {ex.Message}");
            }
        }

        /// <summary>弹出文件对话框加载验证记录</summary>
        private async Task LoadVerificationRecordAsync()
        {
            try
            {
                var configDir = GetVerificationDirectory(SelectedSystemNumber);
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    InitialDirectory = configDir
                };

                if (dialog.ShowDialog() != true) return;
                await LoadFromPathAsync(dialog.FileName);
            }
            catch (Exception ex)
            {
                AddLog(L("NeedleVerify_Log_LoadError", "加载失败: {0}"), ex.Message);
                _logger?.Error($"[NeedleVerify] 加载验证记录失败: {ex.Message}");
            }
        }

        /// <summary>从指定路径加载验证记录</summary>
        private async Task LoadFromPathAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                AddLog(L("NeedleVerify_Log_LoadNotFound", "文件不存在: {0}"), filePath);
                return;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var record = JsonConvert.DeserializeObject<NeedleVerifyRecord>(json);
            if (record == null) return;

            ApplyRecord(record);
            CurrentFilePath = filePath;
            CurrentFileName = Path.GetFileName(filePath);
            AddLog(L("NeedleVerify_Log_LoadSuccess", "已加载: {0}"), CurrentFileName);
            _logger?.Info($"[NeedleVerify] 系统{SelectedSystemNumber}验证记录已加载: {filePath}");
        }

        /// <summary>尝试从配方池或目录最新文件自动加载</summary>
        private async Task TryAutoLoadConfigAsync()
        {
            try
            {
                var poolName = _recipePoolService?.CurrentPoolName ?? "Default";
                var extKey = $"NeedleVerify_CurrentFile_System{SelectedSystemNumber}";
                var extData = await _recipePoolService.GetExtensionDataAsync<NeedleVerifyFileRecord>(poolName, extKey);

                if (extData?.FilePath != null && File.Exists(extData.FilePath))
                {
                    _logger?.Info($"[NeedleVerify] 从配方池记录加载: {extData.FilePath}");
                    await LoadFromPathAsync(extData.FilePath);
                    return;
                }

                var configDir = GetVerificationDirectory(SelectedSystemNumber);
                var latest = Directory
                    .EnumerateFiles(configDir, $"NeedleVerify_System{SelectedSystemNumber}_*.json")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();

                if (latest != null)
                {
                    _logger?.Info($"[NeedleVerify] 配方池无记录，加载最新文件: {latest}");
                    await LoadFromPathAsync(latest);
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[NeedleVerify] 自动加载验证记录失败: {ex.Message}");
            }
        }

        private NeedleVerifyRecord BuildCurrentRecord()
        {
            return new NeedleVerifyRecord
            {
                SystemNumber = SelectedSystemNumber,
                SavedAt = DateTime.Now,
                MeasuredX = MeasuredX,
                MeasuredY = MeasuredY,
                MeasuredZ = MeasuredZ,
                ActiveTcpX = ActiveTcpX,
                ActiveTcpY = ActiveTcpY,
                ActiveTcpZ = ActiveTcpZ,
                EffectiveX = EffectiveX,
                EffectiveY = EffectiveY,
                EffectiveZ = EffectiveZ,
                ReferenceX = ReferenceX,
                ReferenceY = ReferenceY,
                ReferenceZ = ReferenceZ,
                DeviationX = DeviationX,
                DeviationY = DeviationY,
                DeviationZ = DeviationZ,
                ResultX = ResultX,
                ResultY = ResultY,
                ResultZ = ResultZ,
                OverallResult = OverallResult,
                LastReportSummary = LastReportSummary ?? BuildReportSummary(DateTime.Now),
                VerificationLogs = VerificationLogs.ToList()
            };
        }

        private void ApplyRecord(NeedleVerifyRecord record)
        {
            MeasuredX = record.MeasuredX;
            MeasuredY = record.MeasuredY;
            MeasuredZ = record.MeasuredZ;
            ActiveTcpX = record.ActiveTcpX;
            ActiveTcpY = record.ActiveTcpY;
            ActiveTcpZ = record.ActiveTcpZ;
            EffectiveX = record.EffectiveX;
            EffectiveY = record.EffectiveY;
            EffectiveZ = record.EffectiveZ;
            ReferenceX = record.ReferenceX;
            ReferenceY = record.ReferenceY;
            ReferenceZ = record.ReferenceZ;

            DeviationX = record.DeviationX;
            DeviationY = record.DeviationY;
            DeviationZ = record.DeviationZ;

            var (xResult, xColor) = EvaluateDeviation(DeviationX);
            var (yResult, yColor) = EvaluateDeviation(DeviationY);
            var (zResult, zColor) = EvaluateDeviation(DeviationZ);

            ResultX = string.IsNullOrWhiteSpace(record.ResultX) ? xResult : record.ResultX;
            ResultY = string.IsNullOrWhiteSpace(record.ResultY) ? yResult : record.ResultY;
            ResultZ = string.IsNullOrWhiteSpace(record.ResultZ) ? zResult : record.ResultZ;
            ResultXColor = xColor;
            ResultYColor = yColor;
            ResultZColor = zColor;

            // 综合结果取 X/Y/Z 最差值，避免回退到单一轴导致误判
            var worstDeviation = new[] { DeviationX, DeviationY, DeviationZ }.Max();
            var (overallResultText, overallColor) = EvaluateDeviation(worstDeviation);
            OverallResult = string.IsNullOrWhiteSpace(record.OverallResult) ? overallResultText : record.OverallResult;
            OverallResultColor = overallColor;

            LastReportSummary = record.LastReportSummary ?? BuildReportSummary(record.SavedAt);

            VerificationLogs.Clear();
            if (record.VerificationLogs != null)
            {
                foreach (var log in record.VerificationLogs)
                    VerificationLogs.Add(log);
            }
        }

        private string BuildReportSummary(DateTime timestamp)
        {
            var measuredLabel = L("NeedleVerify_Measured", "实测");
            var tcpLabel = L("NeedleVerify_ActiveTcp", "已生效TCP");
            var effectiveLabel = L("NeedleVerify_Effective", "有效坐标");
            var refLabel = L("NeedleVerify_Reference", "固定基准");
            var devLabel = L("NeedleVerify_Report_Deviation", "偏差");

            return $"===== {L("NeedleVerify_Report_Title", "针头校准验证报告")} =====\n" +
                   $"{L("NeedleVerify_Report_Time", "时间")}: {timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                   $"{L("NeedleVerify_Report_System", "系统")}: {SelectedSystemNumber}\n" +
                   $"----------------------------------------\n" +
                   $"X: {measuredLabel}={MeasuredX:F4}  {tcpLabel}={ActiveTcpX:F4}  {effectiveLabel}={EffectiveX:F4}  {refLabel}={ReferenceX:F4}  {devLabel}={DeviationX:F4}mm [{ResultX}]\n" +
                   $"Y: {measuredLabel}={MeasuredY:F4}  {tcpLabel}={ActiveTcpY:F4}  {effectiveLabel}={EffectiveY:F4}  {refLabel}={ReferenceY:F4}  {devLabel}={DeviationY:F4}mm [{ResultY}]\n" +
                   $"Z: {measuredLabel}={MeasuredZ:F4}  {tcpLabel}={ActiveTcpZ:F4}  {effectiveLabel}={EffectiveZ:F4}  {refLabel}={ReferenceZ:F4}  {devLabel}={DeviationZ:F4}mm [{ResultZ}]\n" +
                   $"----------------------------------------\n" +
                   $"{L("NeedleVerify_Report_Overall", "综合结果")}: {OverallResult}\n" +
                   $"========================================\n";
        }

        private async Task SaveCurrentFileToRecipePoolAsync()
        {
            try
            {
                var poolName = _recipePoolService?.CurrentPoolName ?? "Default";
                await _recipePoolService.SetExtensionDataAsync(poolName,
                    $"NeedleVerify_CurrentFile_System{SelectedSystemNumber}",
                    new NeedleVerifyFileRecord { FilePath = CurrentFilePath });
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[NeedleVerify] 保存文件记录到配方池失败: {ex.Message}");
            }
        }

        /// <summary>获取 NeedleTcp 校准参数目录：Config/Calibration/System{N}</summary>
        private static string GetCalibrationDirectory(int systemNumber)
        {
            var dir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config", "Calibration", $"System{systemNumber}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>获取验证记录目录：Config/Calibration/Verification/System{N}</summary>
        private static string GetVerificationDirectory(int systemNumber)
        {
            var dir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config", "Calibration", "Verification", $"System{systemNumber}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>后台异步清理过期验证记录</summary>
        private void QueueCleanupOldConfigFiles(string configDir, string currentFilePath, int systemNumber)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var cutoff = DateTime.Now.AddDays(-ConfigRetentionDays);
                    var cleanedCount = 0;

                    foreach (var file in Directory.EnumerateFiles(configDir, $"NeedleVerify_System{systemNumber}_*.json"))
                    {
                        if (string.Equals(file, currentFilePath, StringComparison.OrdinalIgnoreCase))
                            continue;

                        try
                        {
                            if (File.GetLastWriteTime(file) >= cutoff)
                                continue;

                            File.Delete(file);
                            cleanedCount++;
                            _logger?.Info($"[NeedleVerify] 已清理过期验证记录: {file}");
                        }
                        catch (Exception ex)
                        {
                            _logger?.Warn($"[NeedleVerify] 清理文件失败: {file}, {ex.Message}");
                        }
                    }

                    if (cleanedCount > 0)
                        _logger?.Info($"[NeedleVerify] 本次清理了 {cleanedCount} 个过期文件 (保留{ConfigRetentionDays}天)");
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"[NeedleVerify] 清理过期文件异常: {ex.Message}");
                }
            });
        }

        private (string result, Brush color) EvaluateDeviation(double deviation)
        {
            if (deviation <= 0.05)
                return (L("NeedleVerify_Result_Pass", "通过"), new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)));
            if (deviation <= 0.15)
                return (L("NeedleVerify_Result_Warning", "警告"), new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)));
            return (L("NeedleVerify_Result_Fail", "失败"), new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)));
        }

        private void ClearLog()
        {
            VerificationLogs.Clear();
        }

        private void AddLog(string format, params string[] args)
        {
            try
            {
                var message = args.Length > 0 ? string.Format(format, args) : format;
                var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
                // 使用 BeginInvoke 异步派发，避免 UI 线程阻塞时死锁
                Application.Current?.Dispatcher.BeginInvoke(() => VerificationLogs.Add(timestamped));
            }
            catch
            {
            }
        }

        private void ResetResults()
        {
            MeasuredX = MeasuredY = MeasuredZ = 0;
            EffectiveX = EffectiveY = EffectiveZ = 0;
            DeviationX = DeviationY = DeviationZ = 0;
            ResultX = ResultY = ResultZ = "-";
            ResultXColor = Brushes.Gray;
            ResultYColor = Brushes.Gray;
            ResultZColor = Brushes.Gray;
            OverallResult = "-";
            OverallResultColor = Brushes.Gray;
        }

        private string L(string key, string fallback) => _localization.GetResourceOrDefault(key, fallback);
    }

    /// <summary>验证记录 JSON 模型</summary>
    public class NeedleVerifyRecord
    {
        public int SystemNumber { get; set; }
        public DateTime SavedAt { get; set; }
        public double MeasuredX { get; set; }
        public double MeasuredY { get; set; }
        public double MeasuredZ { get; set; }
        public double ActiveTcpX { get; set; }
        public double ActiveTcpY { get; set; }
        public double ActiveTcpZ { get; set; }
        public double EffectiveX { get; set; }
        public double EffectiveY { get; set; }
        public double EffectiveZ { get; set; }
        public double ReferenceX { get; set; }
        public double ReferenceY { get; set; }
        public double ReferenceZ { get; set; }
        public double DeviationX { get; set; }
        public double DeviationY { get; set; }
        public double DeviationZ { get; set; }
        public string ResultX { get; set; }
        public string ResultY { get; set; }
        public string ResultZ { get; set; }
        public string OverallResult { get; set; }
        public string LastReportSummary { get; set; }
        public List<string> VerificationLogs { get; set; } = new();
    }

    /// <summary>记录最后使用的验证记录文件路径</summary>
    public class NeedleVerifyFileRecord
    {
        public string FilePath { get; set; }
    }
}
