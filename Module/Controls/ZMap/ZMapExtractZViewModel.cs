using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Core.Abstraction;
using Core.Models;
using Core.Services;
using Microsoft.Win32;
using MotionControl.Interfaces;
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
        private List<CadPoint> _targetPoints;
        private readonly DispenseSegment _segment;
        private readonly IMotionService _motionService;
        private readonly Action<int> _resampleSegment;
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
            DispenseSegment segment,
            IMotionService motionService,
            Action<int> resampleSegment,
            Window dialogWindow)
        {
            _zMapService = zMapService;
            _zMapConfigService = zMapConfigService;
            _segment = segment ?? throw new ArgumentNullException(nameof(segment));
            _motionService = motionService;
            _resampleSegment = resampleSegment;
            _targetPoints = _segment.Points ?? new List<CadPoint>();
            _dialogWindow = dialogWindow;
            SegmentId = _segment.SegmentId ?? string.Empty;

            RoiTypeOptions.Add(new ZMapRoiTypeOption
                { Type = ZMapRoiType.Line, DisplayName = L("ZMap_RoiType_Line") });
            RoiTypeOptions.Add(new ZMapRoiTypeOption
                { Type = ZMapRoiType.CircularArc, DisplayName = L("ZMap_RoiType_Arc") });
            RoiTypeOptions.Add(new ZMapRoiTypeOption
                { Type = ZMapRoiType.Polyline, DisplayName = L("ZMap_RoiType_Polyline") });
            RoiTypeOptions.Add(new ZMapRoiTypeOption
                { Type = ZMapRoiType.SinglePoint, DisplayName = L("ZMap_RoiType_SinglePoint") });
            SelectedRoiTypeOption = RoiTypeOptions[2];

            // 传入选中段点数：单点示教需与之一致，折线/直线/圆弧按此数量重采样
            (_dialogWindow as ZMapExtractZWindow)?.SetTargetPointCount(_targetPoints.Count);
            TrajectoryPointCount = _segment.SamplePointCount >= 2 ? _segment.SamplePointCount : _targetPoints.Count;

            LoadHeightMapCommand = new DelegateCommand(ExecuteLoadHeightMap);
            AddCalibrationPointCommand = new DelegateCommand(ExecuteAddCalibrationPoint);
            RemoveCalibrationPointCommand = new DelegateCommand<ZMapCalibrationPoint>(ExecuteRemoveCalibrationPoint);
            PickCalibrationPixelCommand = new DelegateCommand<ZMapCalibrationPoint>(ExecutePickCalibrationPixel, _ => IsHeightMapLoaded);
            TeachCalibrationMachineCommand = new DelegateCommand<ZMapCalibrationPoint>(ExecuteTeachCalibrationMachine);
            ComputeCalibrationCommand = new DelegateCommand(ExecuteComputeCalibration, () => CalibrationPoints.Count >= 3);
            CalibrateZOffsetCommand = new DelegateCommand(ExecuteCalibrateZOffset, () => IsHeightMapLoaded);
            ApplyTrajectoryPointCountCommand = new DelegateCommand(ExecuteApplyTrajectoryPointCount, () => TrajectoryPointCount >= 2);
            // ROI与DXF轨迹一致的工艺前提下，仅需原始图像像素高度，不再依赖机械XY仿射标定。
            ExtractPreviewCommand = new DelegateCommand(ExecuteExtractPreview, () => IsHeightMapLoaded);
            // 只要有标定点或已完成标定即可保存；关闭窗口时也会自动固化到段级配置
            SaveCalibrationCommand = new DelegateCommand(ExecuteSaveCalibration, () => CalibrationPoints.Count > 0 || HasCalibration);
            ApplyCommand = new DelegateCommand(ExecuteApply, () => PreviewResults.Any(r => r.IsValid));
            CancelCommand = new DelegateCommand(() =>
            {
                // 取消只表示不写回CadPoint.Z；标定/ROI仍固化到本段，便于下次打开恢复
                PersistProfileSilently();
                _dialogWindow.DialogResult = false;
            });

            if (_dialogWindow != null)
            {
                _dialogWindow.Closing += (_, _) => PersistProfileSilently();
                // 窗口显示完成后再自动提取一次，确保Halcon已加载高度图与ROI。
                _dialogWindow.Loaded += OnDialogLoadedAutoPreview;
            }

            RestoreSegmentProfile();
        }

        /// <summary>打开窗口后自动执行一次提取预览；结果不合格时弹窗提醒。</summary>
        private void OnDialogLoadedAutoPreview(object sender, RoutedEventArgs e)
        {
            if (_dialogWindow != null)
                _dialogWindow.Loaded -= OnDialogLoadedAutoPreview;

            _dialogWindow?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsHeightMapLoaded) return;
                ExecuteExtractPreview();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public string SegmentId { get; }

        private int _trajectoryPointCount;
        /// <summary>
        /// 连续轨迹实际离散点数。应用后调用主面板既有离散化逻辑重建CadPoint，
        /// 保证ROI采样数、CadPoint.Z和运动插补点数严格一致。
        /// </summary>
        public int TrajectoryPointCount
        {
            get => _trajectoryPointCount;
            set
            {
                if (SetProperty(ref _trajectoryPointCount, value))
                    ApplyTrajectoryPointCountCommand?.RaiseCanExecuteChanged();
            }
        }

        public DelegateCommand ApplyTrajectoryPointCountCommand { get; }

        public ObservableCollection<ZMapRoiTypeOption> RoiTypeOptions { get; } = new();

        private ZMapRoiTypeOption _selectedRoiTypeOption;
        public ZMapRoiTypeOption SelectedRoiTypeOption
        {
            get => _selectedRoiTypeOption;
            set
            {
                // 切换ROI类型时通知Halcon窗口重种交互ROI（图像未加载则窗口内部忽略）
                if (SetProperty(ref _selectedRoiTypeOption, value) && value != null)
                    (_dialogWindow as ZMapExtractZWindow)?.SetRoiType(value.Type);
            }
        }

        #region 高度图加载

        /// <summary>调用Step3既有按点数重离散化逻辑，并同步本工具的目标点引用。</summary>
        private void ExecuteApplyTrajectoryPointCount()
        {
            if (_resampleSegment == null || TrajectoryPointCount < 2) return;
            _resampleSegment(TrajectoryPointCount);
            _targetPoints = _segment.Points ?? new List<CadPoint>();
            (_dialogWindow as ZMapExtractZWindow)?.SetTargetPointCount(_targetPoints.Count);
            _segment.ZMapProfile.TrajectoryPointCount = _targetPoints.Count;
            PreviewResults.Clear();
            PreviewSummaryText = string.Format(L("ZMap_Status_TrajectoryResampled"), _targetPoints.Count);
            ApplyCommand.RaiseCanExecuteChanged();
        }

        private string _heightMapFilePath = string.Empty;
        public string HeightMapFilePath { get => _heightMapFilePath; set => SetProperty(ref _heightMapFilePath, value); }

        private bool _isHeightMapLoaded;
        public bool IsHeightMapLoaded { get => _isHeightMapLoaded; set => SetProperty(ref _isHeightMapLoaded, value); }

        private string _heightMapStatusText = L("ZMap_Status_NotLoaded");
        public string HeightMapStatusText { get => _heightMapStatusText; set => SetProperty(ref _heightMapStatusText, value); }

        public DelegateCommand LoadHeightMapCommand { get; }

        private void ExecuteLoadHeightMap()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "ZMAP高度图 (*.tif;*.tiff)|*.tif;*.tiff|普通图片-测试 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*",
                Title = L("ZMap_Dialog_SelectHeightMap")
            };
            if (dialog.ShowDialog() != true) return;

            LoadHeightMapFromFile(dialog.FileName);
        }

        /// <summary>
        /// 统一处理手动选择和配置恢复的高度图加载，确保二者共用 GrabImageReader 的读图链路
        /// 以及相同的多语言错误提示与 HALCON 显示刷新逻辑。
        /// </summary>
        private bool LoadHeightMapFromFile(string filePath)
        {
            if (_zMapService.LoadHeightMap(filePath, out string error))
            {
                HeightMapFilePath = filePath;
                IsHeightMapLoaded = true;
                HeightMapStatusText = string.Format(L("ZMap_Status_Loaded"), _zMapService.HeightMapWidth, _zMapService.HeightMapHeight);
                // 进程内用Halcon窗口显示高度图并生成默认ROI（与点胶工具一致）
                (_dialogWindow as ZMapExtractZWindow)?.ShowHeightMap(
                    _zMapService.GetDisplayImage(),
                    SelectedRoiTypeOption?.Type ?? ZMapRoiType.Line);
            }
            else
            {
                IsHeightMapLoaded = false;
                HeightMapStatusText = string.Format(L("ZMap_Status_LoadFailed"), LocalizeLoadError(error));
            }

            ExtractPreviewCommand.RaiseCanExecuteChanged();
            CalibrateZOffsetCommand.RaiseCanExecuteChanged();
            PickCalibrationPixelCommand.RaiseCanExecuteChanged();
            return IsHeightMapLoaded;
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
        /// <summary>进入图像画布像素点拾取模式，下一次左键单击自动填充该行PixelCol/PixelRow。</summary>
        public DelegateCommand<ZMapCalibrationPoint> PickCalibrationPixelCommand { get; }
        /// <summary>读取Dx(8)/Dy(6)当前位置，填充该行机械坐标；仍允许用户手动编辑修正。</summary>
        public DelegateCommand<ZMapCalibrationPoint> TeachCalibrationMachineCommand { get; }
        public DelegateCommand ComputeCalibrationCommand { get; }
        public DelegateCommand SaveCalibrationCommand { get; }

        private void ExecuteAddCalibrationPoint()
        {
            CalibrationPoints.Add(new ZMapCalibrationPoint { Id = CalibrationPoints.Count + 1 });
            ComputeCalibrationCommand.RaiseCanExecuteChanged();
            SaveCalibrationCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteRemoveCalibrationPoint(ZMapCalibrationPoint point)
        {
            if (point == null) return;
            CalibrationPoints.Remove(point);
            RenumberCalibrationPoints();
            ComputeCalibrationCommand.RaiseCanExecuteChanged();
            SaveCalibrationCommand.RaiseCanExecuteChanged();
        }

        private void ExecutePickCalibrationPixel(ZMapCalibrationPoint point)
        {
            if (point == null || !IsHeightMapLoaded) return;
            (_dialogWindow as ZMapExtractZWindow)?.BeginPickCalibrationPixel((col, row) =>
            {
                point.PixelCol = Math.Round(col, 3);
                point.PixelRow = Math.Round(row, 3);
                CalibrationStatusText = string.Format(L("ZMap_Status_PixelPicked"), point.Id, point.PixelCol, point.PixelRow);
            });
        }

        private void ExecuteTeachCalibrationMachine(ZMapCalibrationPoint point)
        {
            if (point == null) return;
            if (_motionService == null)
            {
                CalibrationStatusText = L("ZMap_Status_MachineUnavailable");
                return;
            }
            try
            {
                // 与Step4仿射标定保持一致：Dx(8)/Dy(6)为当前机械实际位置。
                point.MachineX = Math.Round(_motionService.GetAxisState(8).ActualPosition, 3);
                point.MachineY = Math.Round(_motionService.GetAxisState(6).ActualPosition, 3);
                CalibrationStatusText = string.Format(L("ZMap_Status_MachineTaught"), point.Id, point.MachineX, point.MachineY);
            }
            catch (Exception ex)
            {
                CalibrationStatusText = string.Format(L("ZMap_Status_MachineTeachFailed"), ex.Message);
            }
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
            SaveCalibrationCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteSaveCalibration()
        {
            SaveToSegmentProfile();
            CalibrationStatusText = L("ZMap_Status_Saved");
            SaveCalibrationCommand.RaiseCanExecuteChanged();
        }

        /// <summary>关闭/取消时静默固化段级配置，不改动状态栏提示文案。</summary>
        private void PersistProfileSilently()
        {
            try
            {
                bool hasRoi = (_dialogWindow as ZMapExtractZWindow)?.IsRoiComplete == true;
                if (CalibrationPoints.Count == 0 && !HasCalibration && !hasRoi)
                    return;
                SaveToSegmentProfile();
            }
            catch
            {
                // 关闭路径上的固化失败不影响窗口关闭
            }
        }

        /// <summary>优先恢复本段ZMAP配置；旧段没有配置时才读取全局文件作为兼容默认值。</summary>
        private void RestoreSegmentProfile()
        {
            try
            {
                var profile = _segment.ZMapProfile;
                bool useSegment = profile?.IsConfigured == true
                    && profile.CalibrationConfig != null
                    && (profile.CalibrationConfig.CalibrationPoints?.Count > 0
                        || profile.CalibrationConfig.Calibration != null
                        || profile.RoiDefinition?.ControlPoints?.Count > 0);

                var config = useSegment
                    ? profile.CalibrationConfig
                    : _zMapConfigService.Load();

                CalibrationPoints.Clear();
                if (config?.CalibrationPoints != null && config.CalibrationPoints.Count > 0)
                {
                    foreach (var p in config.CalibrationPoints)
                        CalibrationPoints.Add(CloneCalibrationPoint(p));
                }

                _zMapService.ImportConfig(config);
                if (config?.Calibration != null)
                {
                    HasCalibration = true;
                    CalibrationStatusText = string.Format(L("ZMap_Status_Calibrated"),
                        config.Calibration.RmsError, config.Calibration.MaxResidual, config.Calibration.QualityGrade);
                }
                else if (CalibrationPoints.Count > 0)
                {
                    CalibrationStatusText = L("ZMap_Status_NotCalibrated");
                }

                ZOffset = config?.ZOffset ?? _zMapService.ZOffset;
                if (config != null && !string.IsNullOrWhiteSpace(config.LastHeightMapFilePath))
                    HeightMapFilePath = config.LastHeightMapFilePath;

                if (useSegment)
                {
                    if (profile.TrajectoryPointCount >= 2)
                        TrajectoryPointCount = profile.TrajectoryPointCount;

                    SelectedRoiTypeOption = RoiTypeOptions.FirstOrDefault(x => x.Type == profile.RoiDefinition?.Type)
                        ?? SelectedRoiTypeOption;
                    (_dialogWindow as ZMapExtractZWindow)?.ImportRoiDefinition(
                        profile.RoiDefinition, profile.TeachDirection, profile.ReverseRoiDirection);
                }

                // 自动加载上次高度图，否则重开窗口看起来像“数据全空”
                TryRestoreHeightMap(config?.LastHeightMapFilePath);

                ComputeCalibrationCommand.RaiseCanExecuteChanged();
                SaveCalibrationCommand.RaiseCanExecuteChanged();
                ExtractPreviewCommand.RaiseCanExecuteChanged();
                CalibrateZOffsetCommand.RaiseCanExecuteChanged();
                PickCalibrationPixelCommand.RaiseCanExecuteChanged();
            }
            catch
            {
                // 恢复失败不影响窗口正常打开，等待用户重新标定
            }
        }

        /// <summary>若上次高度图文件仍存在则自动加载并恢复ROI显示。</summary>
        private void TryRestoreHeightMap(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
                return;

            LoadHeightMapFromFile(filePath);
        }

        /// <summary>
        /// 将当前标定和ROI写入所属DispenseSegment，并双写全局ZMapCalibration.json作为兜底。
        /// 后续保存segments JSON时段级配置会一并持久化。
        /// </summary>
        private void SaveToSegmentProfile()
        {
            // 以界面上的标定点为准导出，避免仅依赖服务内部_lastCalibrationPoints导致保存空表
            var exported = _zMapService.ExportConfig() ?? new ZMapCalibrationConfig();
            exported.CalibrationPoints = CalibrationPoints.Select(CloneCalibrationPoint).ToList();
            exported.ZOffset = ZOffset;
            if (!string.IsNullOrWhiteSpace(HeightMapFilePath))
                exported.LastHeightMapFilePath = HeightMapFilePath;
            exported.LastUpdatedTime = DateTime.Now;

            var profile = _segment.ZMapProfile ?? new ZMapSegmentProfile();
            profile.IsConfigured = true;
            profile.TrajectoryPointCount = _segment.Points?.Count ?? TrajectoryPointCount;
            profile.CalibrationConfig = exported;
            profile.RoiDefinition = (_dialogWindow as ZMapExtractZWindow)?.ExportRoiDefinition()
                ?? new ZMapRoiDefinition { Type = SelectedRoiTypeOption?.Type ?? ZMapRoiType.Polyline };
            profile.TeachDirection = (ZMapTeachDirection)((_dialogWindow as ZMapExtractZWindow)?.TeachDirectionIndex ?? 0);
            profile.ReverseRoiDirection = (_dialogWindow as ZMapExtractZWindow)?.ReverseRoiDirection ?? false;
            profile.LastUpdatedTime = DateTime.Now;
            _segment.ZMapProfile = profile;

            // 同步回服务内存，保证下次Export一致
            _zMapService.ImportConfig(exported);
            try { _zMapConfigService.Save(exported); } catch { /* 全局兜底失败不影响段级保存 */ }
        }

        private static ZMapCalibrationPoint CloneCalibrationPoint(ZMapCalibrationPoint p) => new ZMapCalibrationPoint
        {
            Id = p.Id,
            PixelCol = p.PixelCol,
            PixelRow = p.PixelRow,
            MachineX = p.MachineX,
            MachineY = p.MachineY,
            Note = p.Note
        };

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

        private ZMapHeightSampleResult _selectedPreviewResult;
        /// <summary>
        /// 右侧提取表当前选中行。选中有效行时，HALCON图像仅高亮同Index的一个提取点；
        /// 无效行或取消选中时清除高亮，避免误认为多个点同时被选中。
        /// </summary>
        public ZMapHeightSampleResult SelectedPreviewResult
        {
            get => _selectedPreviewResult;
            set
            {
                if (!SetProperty(ref _selectedPreviewResult, value)) return;
                int index = value != null && value.IsValid ? value.Index - 1 : -1;
                (_dialogWindow as ZMapExtractZWindow)?.SelectCadPointProjection(index);
            }
        }

        private bool _isPreviewRawData = true;
        /// <summary>
        /// 右侧预览显示模式：原始=图像PixelX/PixelY/RawZ；生成=对应DXF CadX/CadY/待写入Z。
        /// 两种模式共享同一批采样结果，仅切换显示，不重复读取图像。
        /// </summary>
        public bool IsPreviewRawData
        {
            get => _isPreviewRawData;
            set
            {
                if (!SetProperty(ref _isPreviewRawData, value)) return;
                foreach (var result in PreviewResults)
                    result.DisplayRawImageData = value;
            }
        }

        private string _previewSummaryText = string.Empty;
        public string PreviewSummaryText { get => _previewSummaryText; set => SetProperty(ref _previewSummaryText, value); }

        public DelegateCommand ExtractPreviewCommand { get; }
        public DelegateCommand ApplyCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private void ExecuteExtractPreview()
        {
            PreviewResults.Clear();

            // 用户确认“ROI与DXF轨迹一致”：ROI按CadPoint数量等弧长生成图像原始XYZ，
            // 由设置的ROI起始顶点与正/反方向保证与CadPoint列表顺序一致，不使用机械XY/仿射标定。
            if (_targetPoints.Count == 0)
            {
                PreviewSummaryText = L("ZMap_Preview_NoCadPoints");
                RecordExtractQualityToProfile(Array.Empty<ZMapHeightSampleResult>());
                ApplyCommand.RaiseCanExecuteChanged();
                WarnIfPreviewUnqualified(0, 0);
                return;
            }

            var roiWindow = _dialogWindow as ZMapExtractZWindow;
            var pixelPoints = roiWindow?.GetRoiSamplePoints(_targetPoints.Count);
            if (pixelPoints == null || pixelPoints.Count != _targetPoints.Count)
            {
                PreviewSummaryText = L("ZMap_Preview_RoiRequired");
                // ROI未就绪记为“已尝试提取但全部不合格”，避免Step6误以为可安全跟随。
                var failProfile = _segment.ZMapProfile ?? new ZMapSegmentProfile();
                failProfile.LastExtractTotalCount = _targetPoints.Count;
                failProfile.LastExtractValidCount = 0;
                failProfile.LastInvalidZIndices = Enumerable.Range(1, Math.Max(_targetPoints.Count, 0)).ToList();
                failProfile.LastExtractSummary = PreviewSummaryText;
                failProfile.IsConfigured = true;
                failProfile.LastUpdatedTime = DateTime.Now;
                _segment.ZMapProfile = failProfile;
                ApplyCommand.RaiseCanExecuteChanged();
                WarnIfPreviewUnqualified(0, 0);
                return;
            }

            var results = new List<ZMapHeightSampleResult>(_targetPoints.Count);
            for (int i = 0; i < _targetPoints.Count; i++)
            {
                var cadPoint = _targetPoints[i];
                var pixelPoint = pixelPoints[i];
                var result = new ZMapHeightSampleResult
                {
                    Index = i + 1,
                    PixelCol = Math.Round(pixelPoint.Col, 3),
                    PixelRow = Math.Round(pixelPoint.Row, 3),
                    CadX = cadPoint.X,
                    CadY = cadPoint.Y,
                    DisplayRawImageData = IsPreviewRawData
                };
                if (_zMapService.TrySampleRawHeightAtPixel(pixelPoint.Col, pixelPoint.Row, out double rawZ))
                {
                    result.RawZ = Math.Round(rawZ, 3);
                    // 不使用机械Z基准标定，生成的DXF Z即图像原始高度值。
                    result.CorrectedZ = result.RawZ;
                    result.IsValid = true;
                }
                else
                {
                    result.ErrorMessage = L("ZMap_Preview_RoiPixelInvalid");
                }
                results.Add(result);
            }

            foreach (var r in results)
                PreviewResults.Add(r);

            // 将ROI等弧长生成的图像点叠加到HALCON窗口，用于检查起始点与方向。
            (_dialogWindow as ZMapExtractZWindow)?.SetCadPointProjection(
                results.Select(r => new ZMapPixelPoint { Col = r.PixelCol, Row = r.PixelRow }));

            // 新一轮预览清除旧表格选中状态，避免高亮落到上一轮的同序号点。
            SelectedPreviewResult = null;

            int validCount = results.Count(r => r.IsValid);
            PreviewSummaryText = string.Format(L("ZMap_Preview_Summary"), validCount, results.Count);

            // 将提取质量写入段配置，供Step6 ExecuteRun预检（即使未点“应用”也要能拦截无效Z）。
            RecordExtractQualityToProfile(results);

            ApplyCommand.RaiseCanExecuteChanged();
            WarnIfPreviewUnqualified(validCount, results.Count);
        }

        /// <summary>
        /// 把本次提取预览的有效/无效统计固化到段级ZMapProfile。
        /// Step6启用Z向校准时据此拦截：预览说明区已提示不合格却仍执行的情况。
        /// </summary>
        private void RecordExtractQualityToProfile(IReadOnlyList<ZMapHeightSampleResult> results)
        {
            var profile = _segment.ZMapProfile ?? new ZMapSegmentProfile();
            profile.LastExtractTotalCount = results?.Count ?? 0;
            profile.LastExtractValidCount = results?.Count(r => r.IsValid) ?? 0;
            profile.LastInvalidZIndices = results?
                .Where(r => !r.IsValid)
                .Select(r => r.Index)
                .ToList() ?? new List<int>();
            profile.LastExtractSummary = PreviewSummaryText ?? string.Empty;
            profile.LastUpdatedTime = DateTime.Now;
            profile.IsConfigured = true;
            _segment.ZMapProfile = profile;
        }

        /// <summary>
        /// 提取结果不合格时弹窗提醒：无点、ROI未就绪、或存在无效采样点。
        /// 说明区文字已更新，弹窗用于强制操作员注意，避免误点“应用到Z列”。
        /// </summary>
        private void WarnIfPreviewUnqualified(int validCount, int totalCount)
        {
            string message = null;
            if (totalCount == 0)
            {
                // 预览未产出任何点（无CadPoint或ROI采样失败）
                if (!string.IsNullOrWhiteSpace(PreviewSummaryText))
                    message = PreviewSummaryText;
            }
            else if (validCount <= 0)
            {
                message = string.Format(L("ZMap_Preview_Warn_AllInvalid"), totalCount);
            }
            else if (validCount < totalCount)
            {
                message = string.Format(L("ZMap_Preview_Warn_PartialInvalid"), validCount, totalCount, totalCount - validCount);
            }

            if (string.IsNullOrEmpty(message)) return;

            MessageBox.Show(
                message,
                L("ZMap_Preview_Warn_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void ExecuteApply()
        {
            // 应用前再次拦截：若存在无效点，确认是否仅写入有效点
            int invalidCount = PreviewResults.Count(r => !r.IsValid);
            if (invalidCount > 0)
            {
                var confirm = MessageBox.Show(
                    string.Format(L("ZMap_Preview_Warn_ApplyPartial"), PreviewResults.Count - invalidCount, invalidCount),
                    L("ZMap_Preview_Warn_Title"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            // ROI采样数与CadPoint数严格一致；仅覆盖有效点Z，无效点标记IsZMapHeightValid=false保留原Z。
            int appliedCount = 0;
            for (int i = 0; i < PreviewResults.Count && i < _targetPoints.Count; i++)
            {
                if (PreviewResults[i].IsValid)
                {
                    _targetPoints[i].Z = PreviewResults[i].CorrectedZ;
                    _targetPoints[i].IsZMapHeightValid = true;
                    appliedCount++;
                }
                else
                {
                    // 无效点不覆盖Z，但显式标记，供Step6 ExecuteRun预检拦截。
                    _targetPoints[i].IsZMapHeightValid = false;
                }
            }

            RecordExtractQualityToProfile(PreviewResults.ToList());

            // 应用Z时同步固化本段ROI与标定；段保存时将自动进入segments JSON。
            SaveToSegmentProfile();
            PreviewSummaryText = string.Format(L("ZMap_Preview_Applied"), appliedCount);
            // 应用后仍保留不合格摘要，便于关闭窗口后Step6读取HasUnresolvedInvalidZ。
            if (PreviewResults.Any(r => !r.IsValid))
            {
                var profile = _segment.ZMapProfile;
                profile.LastExtractSummary = string.Format(L("ZMap_Preview_Warn_PartialInvalid"),
                    appliedCount, PreviewResults.Count, PreviewResults.Count - appliedCount);
            }
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

        /// <summary>服务层返回资源键；资源缺失时保留原始值，避免错误原因完全丢失。</summary>
        private static string LocalizeLoadError(string errorKey)
        {
            if (string.IsNullOrWhiteSpace(errorKey)) return string.Empty;
            string localized = L(errorKey);
            return localized == $"[{errorKey}]" ? errorKey : localized;
        }
    }
}
