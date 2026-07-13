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
    /// 坐标与高度提取顺序对齐参考Plugin.DispensePath：
    /// 在ZMAP图像上绘制ROI并按当前CadPoint数量等距生成像素轨迹点，直接采样像素Z高度，
    /// 再用"ZMAP像素→机械坐标"仿射标定生成预览机械XY。应用阶段只把逐点Z写回
    /// 原CadPoint，既有CAD/机械XY轨迹保持不变。
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

        /// <summary>ROI类型下拉项，显示文本从中英文资源加载。</summary>
        public sealed class ZMapRoiTypeOption
        {
            public ZMapRoiType Type { get; set; }
            public string DisplayName { get; set; }
        }

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

            RoiTypeOptions.Add(new ZMapRoiTypeOption
                { Type = ZMapRoiType.Line, DisplayName = L("ZMap_RoiType_Line") });
            RoiTypeOptions.Add(new ZMapRoiTypeOption
                { Type = ZMapRoiType.CircularArc, DisplayName = L("ZMap_RoiType_Arc") });
            RoiTypeOptions.Add(new ZMapRoiTypeOption
                { Type = ZMapRoiType.Polyline, DisplayName = L("ZMap_RoiType_Polyline") });
            SelectedRoiTypeOption = RoiTypeOptions[2];

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

        public ObservableCollection<ZMapRoiTypeOption> RoiTypeOptions { get; } = new();

        private ZMapRoiTypeOption _selectedRoiTypeOption;
        public ZMapRoiTypeOption SelectedRoiTypeOption
        {
            get => _selectedRoiTypeOption;
            set => SetProperty(ref _selectedRoiTypeOption, value);
        }

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

            // 对齐参考Plugin.DispensePath：ROI按当前CadPoint数量等距采样，先从像素点取Z，
            // 再将同一像素XY正向转换为机械XY。应用时只覆盖CadPoint.Z，不改变既有XY轨迹。
            var roiWindow = _dialogWindow as ZMapExtractZWindow;
            var pixelPoints = roiWindow?.GetRoiSamplePoints(_targetPoints.Count);
            if (pixelPoints == null || pixelPoints.Count != _targetPoints.Count)
            {
                PreviewSummaryText = L("ZMap_Preview_RoiRequired");
                ApplyCommand.RaiseCanExecuteChanged();
                return;
            }

            var results = _zMapService.SamplePixelHeights(pixelPoints);

            foreach (var r in results)
                PreviewResults.Add(r);

            int validCount = results.Count(r => r.IsValid);
            PreviewSummaryText = string.Format(L("ZMap_Preview_Summary"), validCount, results.Count);

            ApplyCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteApply()
        {
            // ROI采样数与CadPoint数严格一致；仅覆盖Z，不改变CAD/机械XY，避免影响现有轨迹。
            int appliedCount = 0;
            for (int i = 0; i < PreviewResults.Count && i < _targetPoints.Count; i++)
            {
                if (!PreviewResults[i].IsValid) continue;
                _targetPoints[i].Z = PreviewResults[i].CorrectedZ;
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
