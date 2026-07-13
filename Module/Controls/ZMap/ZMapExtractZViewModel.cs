using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Core.Abstraction;
using Core.Models;
using Core.Services;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Mvvm;

namespace Module.Controls.ZMap
{
    /// <summary>
    /// ZMAP高度提取悬浮工具窗口 ViewModel——独立扩展模块，不修改Step3现有的CAD Z聚合逻辑。
    ///
    /// 坐标对齐核心思路（详见 IZMapHeightExtractionService 接口注释）：
    /// 传入的轨迹点已经过Step4现有的"CAD→机械"仿射标定得到 MachineX/MachineY，
    /// 本窗口只需再建立一套独立的"ZMAP像素↔机械坐标"仿射标定，即可通过机械坐标这个
    /// 公共参照系，把ZMAP高度图与DXF轨迹点间接对齐，而不需要两个坐标系直接互转。
    ///
    /// 用户确认提取结果后，点击"应用到Z列"才会真正写回 CadPoint.Z（不确认则不影响任何现有数据），
    /// 写回Z后续若启用 Step6 的"Z向纠偏(ZCorrectionEnabled)"，会按 CadPoint.Z 的相对高度差自动纠偏。
    /// </summary>
    public class ZMapExtractZViewModel : BindableBase
    {
        private readonly IZMapHeightExtractionService _zMapService;
        private readonly IZMapConfigService _zMapConfigService;
        private readonly List<CadPoint> _targetPoints;
        private readonly Window _dialogWindow;

        public ZMapExtractZViewModel(
            IZMapHeightExtractionService zMapService,
            IZMapConfigService zMapConfigService,
            List<CadPoint> targetPoints,
            string segmentId,
            Window dialogWindow)
        {
            _zMapService = zMapService;
            _zMapConfigService = zMapConfigService;
            _targetPoints = targetPoints ?? new List<CadPoint>();
            _dialogWindow = dialogWindow;
            SegmentId = segmentId ?? string.Empty;

            LoadHeightMapCommand = new DelegateCommand(ExecuteLoadHeightMap);
            AddCalibrationPointCommand = new DelegateCommand(ExecuteAddCalibrationPoint);
            RemoveCalibrationPointCommand = new DelegateCommand<ZMapCalibrationPoint>(ExecuteRemoveCalibrationPoint);
            ComputeCalibrationCommand = new DelegateCommand(ExecuteComputeCalibration, () => CalibrationPoints.Count >= 3);
            CalibrateZOffsetCommand = new DelegateCommand(ExecuteCalibrateZOffset, () => IsHeightMapLoaded);
            ExtractPreviewCommand = new DelegateCommand(ExecuteExtractPreview, () => IsHeightMapLoaded && HasCalibration);
            SaveCalibrationCommand = new DelegateCommand(ExecuteSaveCalibration, () => HasCalibration);
            ApplyCommand = new DelegateCommand(ExecuteApply, () => PreviewResults.Any(r => r.IsValid));
            CancelCommand = new DelegateCommand(() => { _dialogWindow.DialogResult = false; });

            RestoreSavedCalibration();
        }

        public string SegmentId { get; }

        #region 高度图加载

        private string _heightMapFilePath = string.Empty;
        public string HeightMapFilePath { get => _heightMapFilePath; set => SetProperty(ref _heightMapFilePath, value); }

        private bool _isHeightMapLoaded;
        public bool IsHeightMapLoaded { get => _isHeightMapLoaded; set => SetProperty(ref _isHeightMapLoaded, value); }

        private string _heightMapStatusText = L("ZMap_Status_NotLoaded");
        public string HeightMapStatusText { get => _heightMapStatusText; set => SetProperty(ref _heightMapStatusText, value); }

        private BitmapImage _previewBitmap;
        public BitmapImage PreviewBitmap { get => _previewBitmap; set => SetProperty(ref _previewBitmap, value); }

        public DelegateCommand LoadHeightMapCommand { get; }

        private void ExecuteLoadHeightMap()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "ZMAP高度图 (*.tif;*.tiff)|*.tif;*.tiff|所有文件 (*.*)|*.*",
                Title = L("ZMap_Dialog_SelectHeightMap")
            };
            if (dialog.ShowDialog() != true) return;

            if (_zMapService.LoadHeightMap(dialog.FileName, out string error))
            {
                HeightMapFilePath = dialog.FileName;
                IsHeightMapLoaded = true;
                HeightMapStatusText = string.Format(L("ZMap_Status_Loaded"), _zMapService.HeightMapWidth, _zMapService.HeightMapHeight);
                RefreshPreviewBitmap();
            }
            else
            {
                IsHeightMapLoaded = false;
                HeightMapStatusText = string.Format(L("ZMap_Status_LoadFailed"), error);
            }

            ExtractPreviewCommand.RaiseCanExecuteChanged();
            CalibrateZOffsetCommand.RaiseCanExecuteChanged();
        }

        private void RefreshPreviewBitmap()
        {
            try
            {
                var path = _zMapService.PreviewImagePath;
                if (string.IsNullOrEmpty(path))
                {
                    PreviewBitmap = null;
                    return;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                PreviewBitmap = bitmap;
            }
            catch
            {
                PreviewBitmap = null;
            }
        }

        #endregion

        #region 像素↔机械坐标标定

        public ObservableCollection<ZMapCalibrationPoint> CalibrationPoints { get; } = new();

        private bool _hasCalibration;
        public bool HasCalibration { get => _hasCalibration; set => SetProperty(ref _hasCalibration, value); }

        private string _calibrationStatusText = L("ZMap_Status_NotCalibrated");
        public string CalibrationStatusText { get => _calibrationStatusText; set => SetProperty(ref _calibrationStatusText, value); }

        public DelegateCommand AddCalibrationPointCommand { get; }
        public DelegateCommand<ZMapCalibrationPoint> RemoveCalibrationPointCommand { get; }
        public DelegateCommand ComputeCalibrationCommand { get; }
        public DelegateCommand SaveCalibrationCommand { get; }

        private void ExecuteAddCalibrationPoint()
        {
            CalibrationPoints.Add(new ZMapCalibrationPoint { Id = CalibrationPoints.Count + 1 });
            ComputeCalibrationCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteRemoveCalibrationPoint(ZMapCalibrationPoint point)
        {
            if (point == null) return;
            CalibrationPoints.Remove(point);
            RenumberCalibrationPoints();
            ComputeCalibrationCommand.RaiseCanExecuteChanged();
        }

        private void RenumberCalibrationPoints()
        {
            for (int i = 0; i < CalibrationPoints.Count; i++)
                CalibrationPoints[i].Id = i + 1;
        }

        private void ExecuteComputeCalibration()
        {
            var result = _zMapService.ComputeCalibration(CalibrationPoints.ToList(), out string error);
            if (result != null)
            {
                HasCalibration = true;
                CalibrationStatusText = string.Format(L("ZMap_Status_Calibrated"), result.RmsError, result.MaxResidual, result.QualityGrade);
            }
            else
            {
                HasCalibration = false;
                CalibrationStatusText = string.Format(L("ZMap_Status_CalibrateFailed"), error);
            }

            ExtractPreviewCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteSaveCalibration()
        {
            var config = _zMapService.ExportConfig();
            _zMapConfigService.Save(config);
            CalibrationStatusText = L("ZMap_Status_Saved");
        }

        /// <summary>启动时自动恢复上次保存的标定结果（仅恢复标定数据，不自动加载高度图文件本身）</summary>
        private void RestoreSavedCalibration()
        {
            try
            {
                var config = _zMapConfigService.Load();
                if (config?.CalibrationPoints != null && config.CalibrationPoints.Count > 0)
                {
                    foreach (var p in config.CalibrationPoints)
                        CalibrationPoints.Add(p);
                }

                _zMapService.ImportConfig(config);
                if (config?.Calibration != null)
                {
                    HasCalibration = true;
                    CalibrationStatusText = string.Format(L("ZMap_Status_Calibrated"),
                        config.Calibration.RmsError, config.Calibration.MaxResidual, config.Calibration.QualityGrade);
                }
                ZOffset = _zMapService.ZOffset;
            }
            catch
            {
                // 恢复失败不影响窗口正常打开，等待用户重新标定
            }
        }

        #endregion

        #region Z基准偏移标定

        private double _refPixelCol;
        public double RefPixelCol { get => _refPixelCol; set => SetProperty(ref _refPixelCol, value); }

        private double _refPixelRow;
        public double RefPixelRow { get => _refPixelRow; set => SetProperty(ref _refPixelRow, value); }

        private double _refMachineZ;
        public double RefMachineZ { get => _refMachineZ; set => SetProperty(ref _refMachineZ, value); }

        private double _zOffset;
        public double ZOffset
        {
            get => _zOffset;
            set
            {
                if (SetProperty(ref _zOffset, value))
                    _zMapService.ZOffset = value;
            }
        }

        public DelegateCommand CalibrateZOffsetCommand { get; }

        private void ExecuteCalibrateZOffset()
        {
            if (!_zMapService.TrySampleRawHeightAtPixel(RefPixelCol, RefPixelRow, out double rawZ))
            {
                CalibrationStatusText = L("ZMap_Status_ZOffsetFailed");
                return;
            }

            _zMapService.CalibrateZOffset(RefMachineZ, rawZ);
            ZOffset = _zMapService.ZOffset;
            CalibrationStatusText = string.Format(L("ZMap_Status_ZOffsetDone"), ZOffset);
        }

        #endregion

        #region 提取预览与应用

        public ObservableCollection<ZMapHeightSampleResult> PreviewResults { get; } = new();

        private string _previewSummaryText = string.Empty;
        public string PreviewSummaryText { get => _previewSummaryText; set => SetProperty(ref _previewSummaryText, value); }

        public DelegateCommand ExtractPreviewCommand { get; }
        public DelegateCommand ApplyCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private void ExecuteExtractPreview()
        {
            PreviewResults.Clear();

            // 未完成Step4坐标标定的点无 MachineX/MachineY，无法反查像素位置，单独提示
            var withMachineCoord = _targetPoints.Where(p => p.MachineX.HasValue && p.MachineY.HasValue).ToList();
            var missingCount = _targetPoints.Count - withMachineCoord.Count;

            var machinePoints = withMachineCoord.Select(p => (p.MachineX.Value, p.MachineY.Value));
            var results = _zMapService.SampleHeights(machinePoints);

            foreach (var r in results)
                PreviewResults.Add(r);

            int validCount = results.Count(r => r.IsValid);
            PreviewSummaryText = missingCount > 0
                ? string.Format(L("ZMap_Preview_SummaryWithMissing"), validCount, results.Count, missingCount)
                : string.Format(L("ZMap_Preview_Summary"), validCount, results.Count);

            ApplyCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteApply()
        {
            // 仅把有效点按机械坐标一一对应回写到原始 CadPoint.Z，无效点保留原值不动
            var withMachineCoord = _targetPoints.Where(p => p.MachineX.HasValue && p.MachineY.HasValue).ToList();
            int appliedCount = 0;
            for (int i = 0; i < PreviewResults.Count && i < withMachineCoord.Count; i++)
            {
                if (!PreviewResults[i].IsValid) continue;
                withMachineCoord[i].Z = PreviewResults[i].CorrectedZ;
                appliedCount++;
            }

            PreviewSummaryText = string.Format(L("ZMap_Preview_Applied"), appliedCount);
            _dialogWindow.DialogResult = appliedCount > 0;
        }

        #endregion

        /// <summary>获取多语言文本（便捷方法），未找到资源时返回带方括号的key便于排查</summary>
        private static string L(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var resource = Application.Current?.TryFindResource(key);
            return resource?.ToString() ?? $"[{key}]";
        }
    }
}
