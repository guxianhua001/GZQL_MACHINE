using Core.Abstraction;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
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
        private const double SafeHeightOffset = 50.0;

        private int _selectedSystemNumber = 1;
        public int SelectedSystemNumber
        {
            get => _selectedSystemNumber;
            set => SetProperty(ref _selectedSystemNumber, value);
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

        public ObservableCollection<string> VerificationLogs { get; } = new ObservableCollection<string>();

        public bool CanVerify => !IsVerifying;

        public DelegateCommand ExecuteVerificationCommand { get; }
        public DelegateCommand SaveReportCommand { get; }
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
            NeedleCompensationManager compensationManager)
        {
            _motionController = motionController;
            _parameterStorage = parameterStorage;
            _logger = logger;
            _localization = localization;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _compensationManager = compensationManager;

            ExecuteVerificationCommand = new DelegateCommand(async () => await ExecuteVerificationAsync(), () => CanVerify)
                .ObservesCanExecute(() => CanVerify);
            SaveReportCommand = new DelegateCommand(SaveReport);
            ClearLogCommand = new DelegateCommand(ClearLog);
            System1Command = new DelegateCommand(() => SelectedSystemNumber = 1);
            System2Command = new DelegateCommand(() => SelectedSystemNumber = 2);
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

                // 步骤1：加载校准参数，安全移动到校准器位置
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

                // 步骤2：接触测量获取当前Z高度
                VerificationStatus = _localization.GetResourceOrDefault("NeedleVerify_Status_ZContact", "Z contact measurement...");
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_ZContact", "Executing Z contact measurement..."));

                double currentZ = teachResult.TryGetValue("Z", out var tz) ? tz : 0;

                VerificationProgress = 70;
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_ZMeasured", "Z height measured: Z={0:F4}"), currentZ.ToString("F4"));

                // 步骤3：与参考值比较，计算偏差
                VerificationStatus = _localization.GetResourceOrDefault("NeedleVerify_Status_Comparing", "Comparing with reference...");
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_Comparing", "Comparing with reference values..."));

                double refX = targetX;
                double refY = targetY;
                double refZ = targetZ;

                DeviationX = Math.Abs(currentX - refX);
                DeviationY = Math.Abs(currentY - refY);
                DeviationZ = Math.Abs(currentZ - refZ);

                VerificationProgress = 85;

                // 评估各轴结果
                var (xResult, xColor) = EvaluateDeviation(DeviationX);
                var (yResult, yColor) = EvaluateDeviation(DeviationY);
                var (zResult, zColor) = EvaluateDeviation(DeviationZ);

                ResultX = xResult;
                ResultY = yResult;
                ResultZ = zResult;
                ResultXColor = xColor;
                ResultYColor = yColor;
                ResultZColor = zColor;

                // 确定总体结果（取最差）
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
        }

        /// <summary>
        /// 安全移动到目标位置：先抬升Z轴到安全高度，再水平移动，最后下降Z轴
        /// 防止针头在水平移动过程中碰撞工件或夹具
        /// </summary>
        private async Task MoveToPositionSafelyAsync(string stationId, double targetX, double targetY, double targetZ, double velocity)
        {
            var safeZPositions = new Dictionary<string, double> { { "DispZ", targetZ + SafeHeightOffset } };
            await _motionController.GotoAsync(stationId, safeZPositions, velocity);

            var horizontalPositions = new Dictionary<string, double> { { "DispX", targetX }, { "GantryY", targetY } };
            await _motionController.GotoAsync(stationId, horizontalPositions, velocity);

            var targetZPositions = new Dictionary<string, double> { { "DispZ", targetZ } };
            await _motionController.GotoAsync(stationId, targetZPositions, velocity * 0.5);
        }

        /// <summary>
        /// 评估偏差值，返回结果文本和对应颜色
        /// 偏差≤0.05mm→Pass(绿), 0.05mm<偏差≤0.15mm→Warning(橙), 偏差>0.15mm→Fail(红)
        /// </summary>
        private (string result, Brush color) EvaluateDeviation(double deviation)
        {
            if (deviation <= 0.05)
                return (_localization.GetResourceOrDefault("NeedleVerify_Result_Pass", "Pass"), new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)));
            if (deviation <= 0.15)
                return (_localization.GetResourceOrDefault("NeedleVerify_Result_Warning", "Warning"), new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)));
            return (_localization.GetResourceOrDefault("NeedleVerify_Result_Fail", "Fail"), new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)));
        }

        /// <summary>
        /// 保存验证报告到Config/Calibration/Verification/目录
        /// </summary>
        private void SaveReport()
        {
            try
            {
                var timestamp = DateTime.Now;
                var reportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Calibration", "Verification");
                Directory.CreateDirectory(reportDir);

                var fileName = $"NeedleVerify_Sys{SelectedSystemNumber}_{timestamp:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(reportDir, fileName);

                var report = $"===== {_localization.GetResourceOrDefault("NeedleVerify_Report_Title", "Needle Calibration Verification Report")} =====\n" +
                             $"{_localization.GetResourceOrDefault("NeedleVerify_Report_Time", "Time")}: {timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                             $"{_localization.GetResourceOrDefault("NeedleVerify_Report_Operator", "Operator")}: {_localization.GetResourceOrDefault("NeedleVerify_Report_Unknown", "Unknown")}\n" +
                             $"{_localization.GetResourceOrDefault("NeedleVerify_Report_System", "System")}: {SelectedSystemNumber}\n" +
                             $"----------------------------------------\n" +
                             $"X: {_localization.GetResourceOrDefault("NeedleVerify_Report_Deviation", "Deviation")} = {DeviationX:F4}mm  [{ResultX}]\n" +
                             $"Y: {_localization.GetResourceOrDefault("NeedleVerify_Report_Deviation", "Deviation")} = {DeviationY:F4}mm  [{ResultY}]\n" +
                             $"Z: {_localization.GetResourceOrDefault("NeedleVerify_Report_Deviation", "Deviation")} = {DeviationZ:F4}mm  [{ResultZ}]\n" +
                             $"----------------------------------------\n" +
                             $"{_localization.GetResourceOrDefault("NeedleVerify_Report_Overall", "Overall")}: {OverallResult}\n" +
                             $"========================================\n";

                File.WriteAllText(filePath, report);
                LastReportSummary = report;

                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_ReportSaved", "Report saved: {0}"), fileName);
                _logger?.Info($"Needle verification report saved: {filePath}");
            }
            catch (Exception ex)
            {
                AddLog(_localization.GetResourceOrDefault("NeedleVerify_Log_ReportError", "Report save error: {0}"), ex.Message);
                _logger?.Error($"Needle verification report save failed: {ex.Message}");
            }
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
}
