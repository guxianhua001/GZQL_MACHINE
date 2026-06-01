using Core.Abstraction;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Module.ViewModels
{
    public class NeedleCalibrationVerifyViewModel : BindableBase
    {
        private readonly IPositionMotionController _motionController;
        private readonly IParameterStorage _parameterStorage;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly NeedleCompensationManager _compensationManager;
        private readonly IRecipePoolService _recipePoolService;

        private const double SafeHeightOffset = 50.0;
        /// <summary>验证记录文件保留天数</summary>
        private const int ConfigRetentionDays = 30;

        private int _selectedSystemNumber = 1;
        /// <summary>当前选择的系统编号（1或2）</summary>
        public int SelectedSystemNumber
        {
            get => _selectedSystemNumber;
            set
            {
                if (SetProperty(ref _selectedSystemNumber, value))
                    _ = TryAutoLoadConfigAsync();
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

        private double _deviationX;
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
        public DelegateCommand SaveReportCommand { get; }
        public DelegateCommand SaveParametersCommand { get; }
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
            IPositionMotionController motionController,
            IParameterStorage parameterStorage,
            ILoggerService logger,
            ILocalizationService localization,
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            NeedleCompensationManager compensationManager,
            IRecipePoolService recipePoolService)
        {
            _motionController = motionController;
            _parameterStorage = parameterStorage;
            _logger = logger;
            _localization = localization;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _compensationManager = compensationManager;
            _recipePoolService = recipePoolService;

            ExecuteVerificationCommand = new DelegateCommand(async () => await ExecuteVerificationAsync(), () => CanVerify)
                .ObservesCanExecute(() => CanVerify);
            SaveReportCommand = new DelegateCommand(async () => await SaveVerificationRecordAsync());
            SaveParametersCommand = new DelegateCommand(async () => await SaveVerificationRecordAsync());
            LoadParametersCommand = new DelegateCommand(async () => await LoadVerificationRecordAsync());
            ClearLogCommand = new DelegateCommand(ClearLog);
            System1Command = new DelegateCommand(() => SelectedSystemNumber = 1);
            System2Command = new DelegateCommand(() => SelectedSystemNumber = 2);

            _ = InitializeAsync();
        }

        /// <summary>初始化：自动加载最近验证记录</summary>
        private async Task InitializeAsync()
        {
            await TryAutoLoadConfigAsync();
        }

        /// <summary>
        /// 执行校准验证流程：移动到校准器位置，4点寻边获取XY中心，接触测量Z高度，与参考值比较计算偏差
        /// </summary>
        private async Task ExecuteVerificationAsync()
        {
            IsVerifying = true;
            VerificationProgress = 0;
            VerificationStatus = _localization.GetResourceOrDefault("NeedleVerify_Status_Preparing", "Preparing...");
            ResetResults();

            var verificationSucceeded = false;
            try
            {
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_Start", "Verification started - System {0}"), SelectedSystemNumber.ToString());

                var stationId = $"NeedleCalibration_System{SelectedSystemNumber}";
                if (!_motionController.CanExecuteMotion(stationId))
                {
                    VerificationStatus = _localization.GetResourceOrDefault("NeedleVerify_Status_CannotMove", "Cannot execute motion");
                    AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_CannotMove", "Motion not available for station: {0}"), stationId);
                    return;
                }

                VerificationProgress = 10;
                VerificationStatus = _localization.GetResourceOrDefault("NeedleVerify_Status_MovingToCalibrator", "Moving to calibrator...");
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_MovingToCalibrator", "Moving to calibrator position..."));

                var parameters = _parameterStorage.Load<NeedleCalibrationParams>($"NeedleCalibration_System{SelectedSystemNumber}");
                double targetX = parameters?.ReferenceXYZ?.X ?? 0;
                double targetY = parameters?.ReferenceXYZ?.Y ?? 0;
                double targetZ = parameters?.ReferenceXYZ?.Z ?? 0;
                await MoveToPositionSafelyAsync(stationId, targetX, targetY, targetZ, 10.0);

                VerificationProgress = 30;
                VerificationStatus = _localization.GetResourceOrDefault("NeedleVerify_Status_EdgeSearch", "4-point edge search...");
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_EdgeSearch", "Executing 4-point edge search for XY center..."));

                var teachResult = await _motionController.TeachAsync(stationId);
                double currentX = teachResult.TryGetValue("X", out var tx) ? tx : 0;
                double currentY = teachResult.TryGetValue("Y", out var ty) ? ty : 0;

                VerificationProgress = 50;
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_XYCenter", "XY center measured: X={0:F4}, Y={1:F4}"), currentX.ToString("F4"), currentY.ToString("F4"));

                VerificationStatus = _localization.GetResourceOrDefault("NeedleVerify_Status_ZContact", "Z contact measurement...");
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_ZContact", "Executing Z contact measurement..."));

                double currentZ = teachResult.TryGetValue("Z", out var tz) ? tz : 0;

                VerificationProgress = 70;
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_ZMeasured", "Z height measured: Z={0:F4}"), currentZ.ToString("F4"));

                VerificationStatus = _localization.GetResourceOrDefault("NeedleVerify_Status_Comparing", "Comparing with reference...");
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_Comparing", "Comparing with reference values..."));

                DeviationX = Math.Abs(currentX - targetX);
                DeviationY = Math.Abs(currentY - targetY);
                DeviationZ = Math.Abs(currentZ - targetZ);

                VerificationProgress = 85;

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
                VerificationStatus = _localization.GetResourceOrDefault("NeedleVerify_Status_Completed", "Verification completed");

                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_Completed",
                    "Verification completed - X:{0:F4}({1}) Y:{2:F4}({3}) Z:{4:F4}({5}) Overall:{6}"),
                    DeviationX.ToString("F4"), ResultX,
                    DeviationY.ToString("F4"), ResultY,
                    DeviationZ.ToString("F4"), ResultZ,
                    OverallResult);

                _logger?.Info($"Needle calibration verification completed - System{SelectedSystemNumber}: " +
                              $"dX={DeviationX:F4}({ResultX}) dY={DeviationY:F4}({ResultY}) dZ={DeviationZ:F4}({ResultZ}) Overall={OverallResult}");

                verificationSucceeded = true;
            }
            catch (Exception ex)
            {
                VerificationStatus = _localization.GetResourceOrDefault("NeedleVerify_Status_Error", "Error");
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_Error", "Verification error: {0}"), ex.Message);
                _logger?.Error($"Needle calibration verification failed: {ex.Message}");
            }
            finally
            {
                IsVerifying = false;
            }

            // 验证完成后自动保存到默认路径（时间戳命名）
            if (verificationSucceeded)
                await SaveVerificationRecordAsync();
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

                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_ReportSaved", "Report saved: {0}"), fileName);
                _logger?.Info($"[NeedleVerify] 系统{SelectedSystemNumber}验证记录已保存: {filePath}");
            }
            catch (Exception ex)
            {
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_ReportError", "Report save error: {0}"), ex.Message);
                _logger?.Error($"Needle verification record save failed: {ex.Message}");
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
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_LoadError", "Load error: {0}"), ex.Message);
                _logger?.Error($"[NeedleVerify] 加载验证记录失败: {ex.Message}");
            }
        }

        /// <summary>从指定路径加载验证记录</summary>
        private async Task LoadFromPathAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_LoadNotFound", "File not found: {0}"), filePath);
                return;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var record = JsonConvert.DeserializeObject<NeedleVerifyRecord>(json);
            if (record == null) return;

            ApplyRecord(record);
            CurrentFilePath = filePath;
            CurrentFileName = Path.GetFileName(filePath);
            AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_LoadSuccess", "Loaded: {0}"), CurrentFileName);
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
                    .OrderByDescending(f => File.GetLastWriteTime(f))
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

            OverallResult = string.IsNullOrWhiteSpace(record.OverallResult) ? ResultX : record.OverallResult;
            var worstDeviation = new[] { DeviationX, DeviationY, DeviationZ }.Max();
            var (_, overallColor) = EvaluateDeviation(worstDeviation);
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
            return $"===== {_localization.GetResourceOrDefault("NeedleVerify_Report_Title", "Needle Calibration Verification Report")} =====\n" +
                   $"{_localization.GetResourceOrDefault("NeedleVerify_Report_Time", "Time")}: {timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                   $"{_localization.GetResourceOrDefault("NeedleVerify_Report_System", "System")}: {SelectedSystemNumber}\n" +
                   $"----------------------------------------\n" +
                   $"X: {_localization.GetResourceOrDefault("NeedleVerify_Report_Deviation", "Deviation")} = {DeviationX:F4}mm  [{ResultX}]\n" +
                   $"Y: {_localization.GetResourceOrDefault("NeedleVerify_Report_Deviation", "Deviation")} = {DeviationY:F4}mm  [{ResultY}]\n" +
                   $"Z: {_localization.GetResourceOrDefault("NeedleVerify_Report_Deviation", "Deviation")} = {DeviationZ:F4}mm  [{ResultZ}]\n" +
                   $"----------------------------------------\n" +
                   $"{_localization.GetResourceOrDefault("NeedleVerify_Report_Overall", "Overall")}: {OverallResult}\n" +
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

        private async Task MoveToPositionSafelyAsync(string stationId, double targetX, double targetY, double targetZ, double velocity)
        {
            var safeZPositions = new Dictionary<string, double> { { "DispZ", targetZ + SafeHeightOffset } };
            await _motionController.GotoAsync(stationId, safeZPositions, velocity);

            var horizontalPositions = new Dictionary<string, double> { { "DispX", targetX }, { "GantryY", targetY } };
            await _motionController.GotoAsync(stationId, horizontalPositions, velocity);

            var targetZPositions = new Dictionary<string, double> { { "DispZ", targetZ } };
            await _motionController.GotoAsync(stationId, targetZPositions, velocity * 0.5);
        }

        private (string result, Brush color) EvaluateDeviation(double deviation)
        {
            if (deviation <= 0.05)
                return (_localization.GetResourceOrDefault("NeedleVerify_Result_Pass", "Pass"), new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)));
            if (deviation <= 0.15)
                return (_localization.GetResourceOrDefault("NeedleVerify_Result_Warning", "Warning"), new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)));
            return (_localization.GetResourceOrDefault("NeedleVerify_Result_Fail", "Fail"), new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)));
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
                System.Windows.Application.Current?.Dispatcher.Invoke(() => VerificationLogs.Add(timestamped));
            }
            catch
            {
            }
        }

        private void ResetResults()
        {
            DeviationX = 0;
            DeviationY = 0;
            DeviationZ = 0;
            ResultX = "-";
            ResultY = "-";
            ResultZ = "-";
            ResultXColor = Brushes.Gray;
            ResultYColor = Brushes.Gray;
            ResultZColor = Brushes.Gray;
            OverallResult = "-";
            OverallResultColor = Brushes.Gray;
        }
    }

    /// <summary>验证记录 JSON 模型</summary>
    public class NeedleVerifyRecord
    {
        public int SystemNumber { get; set; }
        public DateTime SavedAt { get; set; }
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
