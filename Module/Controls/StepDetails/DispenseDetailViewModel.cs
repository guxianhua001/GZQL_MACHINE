using Core.Abstraction;
using Core.Events;
using Core.Models;
using Core.Utilities;
using StationTasks.Models;
using StationTasks.Params;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Regions;
using Recipe.Events;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Module.ViewModels
{
    /// <summary>
    /// 点胶步骤详情编辑 ViewModel——管理 DispenseDetail 的模式、校准、默认参数及分段引用
    /// </summary>
    public class DispenseDetailViewModel : BindableBase, INavigationAware
    {
        private readonly IContainerProvider _containerProvider;
        private readonly ILoggerService _logger;
        private readonly IRecipePoolService _recipePoolService;
        private readonly IStationRegistry _stationRegistry;
        private readonly IDispenseSegmentStore _dispenseSegmentStore;
        private readonly IEventAggregator _eventAggregator;
        private bool _syncingFromSelection;
        private ProcessStep _step;

        #region Step 属性

        public ProcessStep Step
        {
            get => _step;
            set
            {
                if (SetProperty(ref _step, value) && value != null)
                {
                    if (_step.DispenseDetail == null)
                        _step.DispenseDetail = new DispenseDetail();
                    _dispenseSegmentStore.CurrentDispenseDetail = _step.DispenseDetail;
                    RaisePropertyChanged(nameof(DispenseMode));
                    RaisePropertyChanged(nameof(IsDotMode));
                    RaisePropertyChanged(nameof(IsArcMode));
                    RaisePropertyChanged(nameof(EnableZCalibration));
                    RaisePropertyChanged(nameof(ZCompensation3D));
                    RaisePropertyChanged(nameof(ZCompensation3DLinkedVar));
                    RaisePropertyChanged(nameof(IsZCompensation3DLinked));
                    RaisePropertyChanged(nameof(ZCompensationCalibrator));
                    RaisePropertyChanged(nameof(ZCompensationCalibratorLinkedVar));
                    RaisePropertyChanged(nameof(IsZCompensationCalibratorLinked));
                    RaisePropertyChanged(nameof(ManualZCompensation));
                    RaisePropertyChanged(nameof(SegmentRefs));
                    RaisePropertyChanged(nameof(DefaultJumpSpeed));
                    RaisePropertyChanged(nameof(DefaultInterpSpeed));
                    RaisePropertyChanged(nameof(DefaultMoveSpeed));
                    RaisePropertyChanged(nameof(DefaultSafeHeight));
                    RaisePropertyChanged(nameof(DefaultApproachHeight));
                    RaisePropertyChanged(nameof(DefaultDispenseAmount));
                    RaisePropertyChanged(nameof(DefaultPreDelay));
                    RaisePropertyChanged(nameof(DefaultPostDelay));
                    RaisePropertyChanged(nameof(DefaultDispensingPressure));
                    RaisePropertyChanged(nameof(DefaultSuckBackTime));
                    RaisePropertyChanged(nameof(DefaultGlueTriggerOffsetMm));
                    RaisePropertyChanged(nameof(DefaultPreDispenseDelay));
                    RaisePropertyChanged(nameof(DefaultDispenseTime));
                    RaisePropertyChanged(nameof(DefaultCornerDecel));
                    RaisePropertyChanged(nameof(DefaultTeachHeight));
                    RaisePropertyChanged(nameof(DefaultHeightCompensation));
                    RaisePropertyChanged(nameof(IsDryRunMode));
                    RaisePropertyChanged(nameof(IsRealDispenseMode));
                    RaisePropertyChanged(nameof(StepDescription));
                }
            }
        }

        public string StepDescription => _step == null ? "—" : $"Seq{_step.Seq} - {_step.CompFeature ?? "—"} → {_step.SiteFeature ?? "—"}";

        #endregion

        #region 模式与校准

        public DispenseStepMode DispenseMode
        {
            get => _step?.DispenseDetail?.DispenseMode ?? DispenseStepMode.Dot;
            set
            {
                if (_step?.DispenseDetail != null) _step.DispenseDetail.DispenseMode = value;
                RaisePropertyChanged(nameof(IsDotMode));
                RaisePropertyChanged(nameof(IsArcMode));
            }
        }

        public bool IsDotMode
        {
            get => DispenseMode == DispenseStepMode.Dot;
            set { if (value) DispenseMode = DispenseStepMode.Dot; }
        }

        public bool IsArcMode
        {
            get => DispenseMode == DispenseStepMode.Arc;
            set { if (value) DispenseMode = DispenseStepMode.Arc; }
        }

        /// <summary>
        /// 点胶针头索引（0=针头1/Dz₂轴, 1=针头2/Dz₃轴）
        /// Dz₁轴为相机/3D扫描轴，不作为点胶轴使用
        /// </summary>
        public int NeedleIndex
        {
            get => _step?.DispenseDetail?.NeedleIndex ?? 0;
            set
            {
                if (_step?.DispenseDetail != null)
                {
                    _step.DispenseDetail.NeedleIndex = value;
                    RaisePropertyChanged(nameof(IsNeedle1Selected));
                    RaisePropertyChanged(nameof(IsNeedle2Selected));
                }
            }
        }

        /// <summary>是否选中针头1（Dz₂轴）</summary>
        public bool IsNeedle1Selected
        {
            get => NeedleIndex == 0;
            set { if (value) NeedleIndex = 0; }
        }

        /// <summary>是否选中针头2（Dz₃轴）</summary>
        public bool IsNeedle2Selected
        {
            get => NeedleIndex == 1;
            set { if (value) NeedleIndex = 1; }
        }

        public bool EnableZCalibration
        {
            get => _step?.DispenseDetail?.EnableZCalibration ?? false;
            set { if (_step?.DispenseDetail != null) _step.DispenseDetail.EnableZCalibration = value; }
        }

        public double ZCompensation3D
        {
            get => _step?.DispenseDetail?.ZCompensation3D ?? 0.0;
            set { if (_step?.DispenseDetail != null) _step.DispenseDetail.ZCompensation3D = value; }
        }

        public string ZCompensation3DLinkedVar
        {
            get => _step?.DispenseDetail?.ZCompensation3DLinkedVar;
            set
            {
                if (_step?.DispenseDetail != null)
                {
                    _step.DispenseDetail.ZCompensation3DLinkedVar = value;
                    RaisePropertyChanged(nameof(IsZCompensation3DLinked));
                    RefreshZCompensationDisplayValues();
                }
            }
        }

        public bool IsZCompensation3DLinked => !string.IsNullOrEmpty(_step?.DispenseDetail?.ZCompensation3DLinkedVar);

        /// <summary>Z补偿(3D)链接全局变量的实时显示值</summary>
        public double ZCompensation3DDisplayValue { get; private set; }

        public double ZCompensationCalibrator
        {
            get => _step?.DispenseDetail?.ZCompensationCalibrator ?? 0.0;
            set { if (_step?.DispenseDetail != null) _step.DispenseDetail.ZCompensationCalibrator = value; }
        }

        public string ZCompensationCalibratorLinkedVar
        {
            get => _step?.DispenseDetail?.ZCompensationCalibratorLinkedVar;
            set
            {
                if (_step?.DispenseDetail != null)
                {
                    _step.DispenseDetail.ZCompensationCalibratorLinkedVar = value;
                    RaisePropertyChanged(nameof(IsZCompensationCalibratorLinked));
                    RefreshZCompensationDisplayValues();
                }
            }
        }

        public bool IsZCompensationCalibratorLinked => !string.IsNullOrEmpty(_step?.DispenseDetail?.ZCompensationCalibratorLinkedVar);

        /// <summary>Z补偿(校准器)链接全局变量的实时显示值</summary>
        public double ZCompensationCalibratorDisplayValue { get; private set; }

        public double ManualZCompensation
        {
            get => _step?.DispenseDetail?.ManualZCompensation ?? 0.0;
            set { if (_step?.DispenseDetail != null) _step.DispenseDetail.ManualZCompensation = value; }
        }

        private ObservableCollection<GlobalVariable> _availableGlobalVariables = new ObservableCollection<GlobalVariable>();
        public ObservableCollection<GlobalVariable> AvailableGlobalVariables
        {
            get => _availableGlobalVariables;
            set => SetProperty(ref _availableGlobalVariables, value);
        }

        public DelegateCommand UnlinkZCompensation3DCommand { get; }
        public DelegateCommand UnlinkZCompensationCalibratorCommand { get; }

        #endregion

        #region 分段引用集合

        public ObservableCollection<DispenseSegmentRef> SegmentRefs => _step?.DispenseDetail?.SegmentRefs;

        private DispenseSegmentRef _selectedSegmentRef;
        public DispenseSegmentRef SelectedSegmentRef
        {
            get => _selectedSegmentRef;
            set
            {
                if (SetProperty(ref _selectedSegmentRef, value))
                {
                    RefreshSelectedRefFromSource();
                    RaisePropertyChanged(nameof(ProcessParamsTitle));
                    RaisePropertyChanged(nameof(ShowOverrideParams));
                    RaisePropertyChanged(nameof(EffectiveJumpSpeed));
                    RaisePropertyChanged(nameof(EffectiveInterpSpeed));
                    RaisePropertyChanged(nameof(EffectiveMoveSpeed));
                    RaisePropertyChanged(nameof(EffectiveSafeHeight));
                    RaisePropertyChanged(nameof(EffectiveApproachHeight));
                    RaisePropertyChanged(nameof(EffectiveCornerDecel));
                    RaisePropertyChanged(nameof(EffectiveDispenseAmount));
                    RaisePropertyChanged(nameof(EffectiveDispenseTime));
                    RaisePropertyChanged(nameof(EffectivePreDelay));
                    RaisePropertyChanged(nameof(EffectivePostDelay));
                    RaisePropertyChanged(nameof(EffectiveDispensingPressure));
                    RaisePropertyChanged(nameof(EffectiveSuckBackTime));
                    RaisePropertyChanged(nameof(EffectiveGlueTriggerOffsetMm));
                    RaisePropertyChanged(nameof(EffectiveTeachHeight));
                    RaisePropertyChanged(nameof(EffectiveHeightCompensation));
                    RaisePropertyChanged(nameof(OverrideJumpSpeed));
                    RaisePropertyChanged(nameof(OverrideInterpSpeed));
                    RaisePropertyChanged(nameof(OverrideSafeHeight));
                    RaisePropertyChanged(nameof(OverrideApproachHeight));
                    RaisePropertyChanged(nameof(OverrideDispenseAmount));
                    RaisePropertyChanged(nameof(OverrideDispenseTime));
                    RaisePropertyChanged(nameof(OverridePreDelay));
                    RaisePropertyChanged(nameof(OverridePostDelay));
                    RaisePropertyChanged(nameof(OverrideDispensingPressure));
                    RaisePropertyChanged(nameof(OverrideSuckBackTime));
                    RaisePropertyChanged(nameof(OverrideGlueTriggerOffsetMm));
                    RaisePropertyChanged(nameof(OverrideCornerDecel));
                    RaisePropertyChanged(nameof(OverrideTeachHeight));
                    RaisePropertyChanged(nameof(OverrideHeightCompensation));
                }
            }
        }

        public bool ShowOverrideParams => _selectedSegmentRef != null && !_selectedSegmentRef.UseDefaultParams;

        #endregion

        #region 默认工艺参数

        public double DefaultJumpSpeed
        {
            get => _step?.DispenseDetail?.DefaultJumpSpeed ?? 20.0;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultJumpSpeed == value) return;
                _step.DispenseDetail.DefaultJumpSpeed = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.JumpSpeed), value);
            }
        }

        public double DefaultInterpSpeed
        {
            get => _step?.DispenseDetail?.DefaultInterpSpeed ?? 10.0;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultInterpSpeed == value) return;
                _step.DispenseDetail.DefaultInterpSpeed = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.InterpSpeed), value);
            }
        }

        public double DefaultMoveSpeed
        {
            get => _step?.DispenseDetail?.DefaultMoveSpeed ?? 10.0;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultMoveSpeed == value) return;
                _step.DispenseDetail.DefaultMoveSpeed = value;
                // 空移速度与运动速度保持一致
                _step.DispenseDetail.DefaultJumpSpeed = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.MoveSpeed), value);
                PublishParamToSelectedSegment(nameof(DispenseSegment.JumpSpeed), value);
            }
        }

        public double DefaultSafeHeight
        {
            get => _step?.DispenseDetail?.DefaultSafeHeight ?? 5.0;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultSafeHeight == value) return;
                _step.DispenseDetail.DefaultSafeHeight = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.SafeHeight), value);
            }
        }

        public double DefaultApproachHeight
        {
            get => _step?.DispenseDetail?.DefaultApproachHeight ?? 3.0;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultApproachHeight == value) return;
                _step.DispenseDetail.DefaultApproachHeight = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.ApproachHeight), value);
            }
        }

        public double DefaultDispenseAmount
        {
            get => _step?.DispenseDetail?.DefaultDispenseAmount ?? 1.0;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultDispenseAmount == value) return;
                _step.DispenseDetail.DefaultDispenseAmount = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.DispenseAmount), value);
            }
        }

        public double DefaultPreDelay
        {
            get => _step?.DispenseDetail?.DefaultPreDelay ?? 0.0;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultPreDelay == value) return;
                _step.DispenseDetail.DefaultPreDelay = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.PreDelay), value);
            }
        }

        public double DefaultPostDelay
        {
            get => _step?.DispenseDetail?.DefaultPostDelay ?? 50.0;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultPostDelay == value) return;
                _step.DispenseDetail.DefaultPostDelay = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.PostDelay), value);
            }
        }

        public double DefaultDispensingPressure
        {
            get => _step?.DispenseDetail?.DefaultDispensingPressure ?? 0.30;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultDispensingPressure == value) return;
                _step.DispenseDetail.DefaultDispensingPressure = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.DispensingPressure), value);
            }
        }

        public double DefaultSuckBackTime
        {
            get => _step?.DispenseDetail?.DefaultSuckBackTime ?? 100.0;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultSuckBackTime == value) return;
                _step.DispenseDetail.DefaultSuckBackTime = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.SuckBackTime), value);
            }
        }

        public double DefaultGlueTriggerOffsetMm
        {
            get => _step?.DispenseDetail?.DefaultGlueTriggerOffsetMm ?? 0.5;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultGlueTriggerOffsetMm == value) return;
                _step.DispenseDetail.DefaultGlueTriggerOffsetMm = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.GlueTriggerOffsetMm), value);
            }
        }

        public double DefaultPreDispenseDelay
        {
            get => _step?.DispenseDetail?.DefaultPreDispenseDelay ?? 50.0;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultPreDispenseDelay == value) return;
                _step.DispenseDetail.DefaultPreDispenseDelay = value;
                // 同步到 DispenseSegment.PreDelay（与 DotProcessParams.PreDispenseDelay 对应）
                PublishParamToSelectedSegment(nameof(DispenseSegment.PreDelay), value);
            }
        }

        public double DefaultDispenseTime
        {
            get => _step?.DispenseDetail?.DefaultDispenseTime ?? 180.0;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultDispenseTime == value) return;
                _step.DispenseDetail.DefaultDispenseTime = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.DispenseTime), value);
            }
        }

        public double DefaultCornerDecel
        {
            get => _step?.DispenseDetail?.DefaultCornerDecel ?? 0.3;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultCornerDecel == value) return;
                _step.DispenseDetail.DefaultCornerDecel = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.CornerDecel), value);
            }
        }

        public double DefaultTeachHeight
        {
            get => _step?.DispenseDetail?.DefaultTeachHeight ?? 0.0;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultTeachHeight == value) return;
                _step.DispenseDetail.DefaultTeachHeight = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.TeachHeight), value);
            }
        }

        public double DefaultHeightCompensation
        {
            get => _step?.DispenseDetail?.DefaultHeightCompensation ?? 0.0;
            set
            {
                if (_step?.DispenseDetail == null) return;
                if (_step.DispenseDetail.DefaultHeightCompensation == value) return;
                _step.DispenseDetail.DefaultHeightCompensation = value;
                PublishParamToSelectedSegment(nameof(DispenseSegment.HeightCompensation), value);
            }
        }

        #endregion

        #region 执行控制

        public bool IsDryRunMode
        {
            get => _step?.DispenseDetail?.IsDryRunMode ?? true;
            set { if (_step?.DispenseDetail != null) _step.DispenseDetail.IsDryRunMode = value; RaisePropertyChanged(nameof(IsRealDispenseMode)); }
        }

        public bool IsRealDispenseMode
        {
            get => _step?.DispenseDetail?.IsRealDispenseMode ?? false;
            set { if (_step?.DispenseDetail != null) _step.DispenseDetail.IsRealDispenseMode = value; RaisePropertyChanged(nameof(IsDryRunMode)); }
        }

        #endregion

        #region 有效工艺参数（Process Parameters 区域绑定——优先 Override，fallback Default）

        public string ProcessParamsTitle => _selectedSegmentRef != null
            ? $"Segment Override Parameters — {_selectedSegmentRef.SourceSegmentId}"
            : "Process Parameters";

        public double EffectiveJumpSpeed
        {
            get => _selectedSegmentRef?.OverrideJumpSpeed ?? _step?.DispenseDetail?.DefaultJumpSpeed ?? 20.0;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideJumpSpeed = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.JumpSpeed), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultJumpSpeed == value) return;
                    _step.DispenseDetail.DefaultJumpSpeed = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.JumpSpeed), value);
                }
            }
        }

        public double EffectiveInterpSpeed
        {
            get => _selectedSegmentRef?.OverrideInterpSpeed ?? _step?.DispenseDetail?.DefaultInterpSpeed ?? 10.0;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideInterpSpeed = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.InterpSpeed), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultInterpSpeed == value) return;
                    _step.DispenseDetail.DefaultInterpSpeed = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.InterpSpeed), value);
                }
            }
        }

        public double EffectiveMoveSpeed
        {
            get => _selectedSegmentRef?.OverrideMoveSpeed ?? _step?.DispenseDetail?.DefaultMoveSpeed ?? 10.0;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideMoveSpeed = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.MoveSpeed), value);
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.JumpSpeed), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultMoveSpeed == value) return;
                    _step.DispenseDetail.DefaultMoveSpeed = value;
                    _step.DispenseDetail.DefaultJumpSpeed = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.MoveSpeed), value);
                    PublishParamToSelectedSegment(nameof(DispenseSegment.JumpSpeed), value);
                }
            }
        }

        public double EffectiveSafeHeight
        {
            get => _selectedSegmentRef?.OverrideSafeHeight ?? _step?.DispenseDetail?.DefaultSafeHeight ?? 5.0;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideSafeHeight = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.SafeHeight), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultSafeHeight == value) return;
                    _step.DispenseDetail.DefaultSafeHeight = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.SafeHeight), value);
                }
            }
        }

        public double EffectiveApproachHeight
        {
            get => _selectedSegmentRef?.OverrideApproachHeight ?? _step?.DispenseDetail?.DefaultApproachHeight ?? 3.0;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideApproachHeight = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.ApproachHeight), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultApproachHeight == value) return;
                    _step.DispenseDetail.DefaultApproachHeight = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.ApproachHeight), value);
                }
            }
        }

        public double EffectiveCornerDecel
        {
            get => _selectedSegmentRef?.OverrideCornerDecel ?? _step?.DispenseDetail?.DefaultCornerDecel ?? 0.3;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideCornerDecel = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.CornerDecel), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultCornerDecel == value) return;
                    _step.DispenseDetail.DefaultCornerDecel = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.CornerDecel), value);
                }
            }
        }

        public double EffectiveDispenseAmount
        {
            get => _selectedSegmentRef?.OverrideDispenseAmount ?? _step?.DispenseDetail?.DefaultDispenseAmount ?? 1.0;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideDispenseAmount = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.DispenseAmount), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultDispenseAmount == value) return;
                    _step.DispenseDetail.DefaultDispenseAmount = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.DispenseAmount), value);
                }
            }
        }

        public double EffectiveDispenseTime
        {
            get => _selectedSegmentRef?.OverrideDispenseTime ?? _step?.DispenseDetail?.DefaultDispenseTime ?? 180.0;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideDispenseTime = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.DispenseTime), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultDispenseTime == value) return;
                    _step.DispenseDetail.DefaultDispenseTime = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.DispenseTime), value);
                }
            }
        }

        public double EffectivePreDelay
        {
            get => _selectedSegmentRef?.OverridePreDelay ?? _step?.DispenseDetail?.DefaultPreDelay ?? 0.0;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverridePreDelay = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.PreDelay), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultPreDelay == value) return;
                    _step.DispenseDetail.DefaultPreDelay = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.PreDelay), value);
                }
            }
        }

        public double EffectivePostDelay
        {
            get => _selectedSegmentRef?.OverridePostDelay ?? _step?.DispenseDetail?.DefaultPostDelay ?? 50.0;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverridePostDelay = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.PostDelay), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultPostDelay == value) return;
                    _step.DispenseDetail.DefaultPostDelay = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.PostDelay), value);
                }
            }
        }

        public double EffectiveDispensingPressure
        {
            get => _selectedSegmentRef?.OverrideDispensingPressure ?? _step?.DispenseDetail?.DefaultDispensingPressure ?? 0.30;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideDispensingPressure = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.DispensingPressure), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultDispensingPressure == value) return;
                    _step.DispenseDetail.DefaultDispensingPressure = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.DispensingPressure), value);
                }
            }
        }

        public double EffectiveSuckBackTime
        {
            get => _selectedSegmentRef?.OverrideSuckBackTime ?? _step?.DispenseDetail?.DefaultSuckBackTime ?? 100.0;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideSuckBackTime = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.SuckBackTime), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultSuckBackTime == value) return;
                    _step.DispenseDetail.DefaultSuckBackTime = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.SuckBackTime), value);
                }
            }
        }

        public double EffectiveGlueTriggerOffsetMm
        {
            get => _selectedSegmentRef?.OverrideGlueTriggerOffsetMm ?? _step?.DispenseDetail?.DefaultGlueTriggerOffsetMm ?? 0.5;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideGlueTriggerOffsetMm = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.GlueTriggerOffsetMm), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultGlueTriggerOffsetMm == value) return;
                    _step.DispenseDetail.DefaultGlueTriggerOffsetMm = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.GlueTriggerOffsetMm), value);
                }
            }
        }

        public double EffectiveTeachHeight
        {
            get => _selectedSegmentRef?.OverrideTeachHeight ?? _step?.DispenseDetail?.DefaultTeachHeight ?? 0.0;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideTeachHeight = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.TeachHeight), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultTeachHeight == value) return;
                    _step.DispenseDetail.DefaultTeachHeight = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.TeachHeight), value);
                }
            }
        }

        public double EffectiveHeightCompensation
        {
            get => _selectedSegmentRef?.OverrideHeightCompensation ?? _step?.DispenseDetail?.DefaultHeightCompensation ?? 0.0;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideHeightCompensation = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.HeightCompensation), value);
                }
                else
                {
                    if (_step?.DispenseDetail == null) return;
                    if (_step.DispenseDetail.DefaultHeightCompensation == value) return;
                    _step.DispenseDetail.DefaultHeightCompensation = value;
                    PublishParamToSelectedSegment(nameof(DispenseSegment.HeightCompensation), value);
                }
            }
        }

        #endregion

        #region 覆盖参数属性（委托到 SelectedSegmentRef）

        public double OverrideJumpSpeed
        {
            get => _selectedSegmentRef?.OverrideJumpSpeed ?? 20.0;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverrideJumpSpeed = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.JumpSpeed), value); } }
        }

        public double OverrideMoveSpeed
        {
            get => _selectedSegmentRef?.OverrideMoveSpeed ?? 10.0;
            set
            {
                if (_selectedSegmentRef != null)
                {
                    _selectedSegmentRef.OverrideMoveSpeed = value;
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.MoveSpeed), value);
                    SyncOverrideToSourceSegment(nameof(DispenseSegment.JumpSpeed), value);
                }
            }
        }

        public double OverrideInterpSpeed
        {
            get => _selectedSegmentRef?.OverrideInterpSpeed ?? 10.0;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverrideInterpSpeed = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.InterpSpeed), value); } }
        }

        public double OverrideSafeHeight
        {
            get => _selectedSegmentRef?.OverrideSafeHeight ?? 5.0;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverrideSafeHeight = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.SafeHeight), value); } }
        }

        public double OverrideApproachHeight
        {
            get => _selectedSegmentRef?.OverrideApproachHeight ?? 3.0;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverrideApproachHeight = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.ApproachHeight), value); } }
        }

        public double OverrideDispenseAmount
        {
            get => _selectedSegmentRef?.OverrideDispenseAmount ?? 1.0;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverrideDispenseAmount = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.DispenseAmount), value); } }
        }

        public double OverrideDispenseTime
        {
            get => _selectedSegmentRef?.OverrideDispenseTime ?? 180.0;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverrideDispenseTime = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.DispenseTime), value); } }
        }

        public double OverridePreDelay
        {
            get => _selectedSegmentRef?.OverridePreDelay ?? 0.0;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverridePreDelay = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.PreDelay), value); } }
        }

        public double OverridePostDelay
        {
            get => _selectedSegmentRef?.OverridePostDelay ?? 50.0;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverridePostDelay = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.PostDelay), value); } }
        }

        public double OverrideDispensingPressure
        {
            get => _selectedSegmentRef?.OverrideDispensingPressure ?? 0.30;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverrideDispensingPressure = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.DispensingPressure), value); } }
        }

        public double OverrideSuckBackTime
        {
            get => _selectedSegmentRef?.OverrideSuckBackTime ?? 100.0;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverrideSuckBackTime = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.SuckBackTime), value); } }
        }

        public double OverrideGlueTriggerOffsetMm
        {
            get => _selectedSegmentRef?.OverrideGlueTriggerOffsetMm ?? 0.5;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverrideGlueTriggerOffsetMm = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.GlueTriggerOffsetMm), value); } }
        }

        public double OverrideCornerDecel
        {
            get => _selectedSegmentRef?.OverrideCornerDecel ?? 0.3;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverrideCornerDecel = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.CornerDecel), value); } }
        }

        public double OverrideTeachHeight
        {
            get => _selectedSegmentRef?.OverrideTeachHeight ?? 0.0;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverrideTeachHeight = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.TeachHeight), value); } }
        }

        public double OverrideHeightCompensation
        {
            get => _selectedSegmentRef?.OverrideHeightCompensation ?? 0.0;
            set { if (_selectedSegmentRef != null) { _selectedSegmentRef.OverrideHeightCompensation = value; SyncOverrideToSourceSegment(nameof(DispenseSegment.HeightCompensation), value); } }
        }

        /// <summary>
        /// 将 Override 参数变更同步回源段（DispenseSegment）
        /// </summary>
        private void SyncOverrideToSourceSegment(string propertyName, double value)
        {
            if (_selectedSegmentRef == null) return;
            var seg = _dispenseSegmentStore?.CurrentSegments?
                .FirstOrDefault(s => s.SegmentId == _selectedSegmentRef.SourceSegmentId);
            if (seg == null) return;

            switch (propertyName)
            {
                case nameof(DispenseSegment.MoveSpeed): seg.MoveSpeed = value; seg.JumpSpeed = value; break;
                case nameof(DispenseSegment.JumpSpeed): seg.JumpSpeed = value; seg.MoveSpeed = value; break;
                case nameof(DispenseSegment.InterpSpeed): seg.InterpSpeed = value; break;
                case nameof(DispenseSegment.SafeHeight): seg.SafeHeight = value; break;
                case nameof(DispenseSegment.ApproachHeight): seg.ApproachHeight = value; break;
                case nameof(DispenseSegment.CornerDecel): seg.CornerDecel = value; break;
                case nameof(DispenseSegment.DispenseAmount): seg.DispenseAmount = value; break;
                case nameof(DispenseSegment.DispenseTime): seg.DispenseTime = value; break;
                case nameof(DispenseSegment.PreDelay): seg.PreDelay = value; break;
                case nameof(DispenseSegment.PostDelay): seg.PostDelay = value; break;
                case nameof(DispenseSegment.DispensingPressure): seg.DispensingPressure = value; break;
                case nameof(DispenseSegment.SuckBackTime): seg.SuckBackTime = value; break;
                case nameof(DispenseSegment.GlueTriggerOffsetMm): seg.GlueTriggerOffsetMm = value; break;
                case nameof(DispenseSegment.TeachHeight): seg.TeachHeight = value; break;
                case nameof(DispenseSegment.HeightCompensation): seg.HeightCompensation = value; break;
            }
        }

        #endregion

        #region 可用源段集合

        private ObservableCollection<DispenseSegment> _availableSourceSegments;
        public ObservableCollection<DispenseSegment> AvailableSourceSegments
        {
            get => _availableSourceSegments;
            set => SetProperty(ref _availableSourceSegments, value);
        }

        #endregion

        #region 命令

        public DelegateCommand ImportLinesCommand { get; }
        public DelegateCommand ImportArcsCommand { get; }
        public DelegateCommand RemoveSelectedCommand { get; }
        public DelegateCommand SelectAllCommand { get; }
        public DelegateCommand InvertSelectionCommand { get; }
        public DelegateCommand CloseCommand { get; }
        public DelegateCommand SaveCommand { get; }

        #endregion

        #region 构造函数

        public DispenseDetailViewModel(
            IContainerProvider containerProvider,
            ILoggerService logger,
            IRecipePoolService recipePoolService,
            IStationRegistry stationRegistry,
            IDispenseSegmentStore dispenseSegmentStore,
            IEventAggregator eventAggregator)
        {
            _containerProvider = containerProvider;
            _logger = logger;
            _recipePoolService = recipePoolService;
            _stationRegistry = stationRegistry;
            _dispenseSegmentStore = dispenseSegmentStore;
            _eventAggregator = eventAggregator;

            _eventAggregator?.GetEvent<SegmentParamChangedEvent>().Subscribe(
                OnSegmentParamChanged, ThreadOption.PublisherThread, false);

            _eventAggregator?.GetEvent<SelectedSegmentChangedEvent>().Subscribe(
                OnSelectedSegmentChanged, ThreadOption.PublisherThread, false);

            _eventAggregator?.GetEvent<ProcessParamsSyncEvent>().Subscribe(
                OnProcessParamsSynced, ThreadOption.PublisherThread, false);

            _eventAggregator?.GetEvent<GlobalVariablesChangedEvent>().Subscribe(
                OnGlobalVariablesChanged, ThreadOption.UIThread);

            ImportLinesCommand = new DelegateCommand(OnImportLines);
            ImportArcsCommand = new DelegateCommand(OnImportArcs);
            RemoveSelectedCommand = new DelegateCommand(OnRemoveSelected);
            SelectAllCommand = new DelegateCommand(OnSelectAll);
            InvertSelectionCommand = new DelegateCommand(OnInvertSelection);
            CloseCommand = new DelegateCommand(OnClose);
            SaveCommand = new DelegateCommand(OnSave);

            UnlinkZCompensation3DCommand = new DelegateCommand(() => ZCompensation3DLinkedVar = null);
            UnlinkZCompensationCalibratorCommand = new DelegateCommand(() => ZCompensationCalibratorLinkedVar = null);

            _ = LoadGlobalVariablesAsync().ConfigureAwait(false);
        }

        #endregion

        #region 反向同步——Step3EditParamsPanel 段参数变更回写 DispenseDetail

        /// <summary>
        /// 响应 Step3EditParamsPanel 段参数变更——反向同步到 DispenseDetail 默认参数
        /// </summary>
        private void OnSegmentParamChanged(SegmentParamPayload payload)
        {
            if (_syncingFromSelection) return;
            if (_step?.DispenseDetail == null || payload.Segment == null) return;

            SyncSegmentRefOverride(payload.Segment, payload.PropertyName);

            switch (payload.PropertyName)
            {
                case nameof(DispenseSegment.JumpSpeed):
                case nameof(DispenseSegment.MoveSpeed):
                    _step.DispenseDetail.DefaultMoveSpeed = payload.Segment.MoveSpeed;
                    _step.DispenseDetail.DefaultJumpSpeed = payload.Segment.MoveSpeed;
                    RaisePropertyChanged(nameof(DefaultMoveSpeed));
                    RaisePropertyChanged(nameof(DefaultJumpSpeed));
                    break;
                case nameof(DispenseSegment.InterpSpeed):
                    _step.DispenseDetail.DefaultInterpSpeed = payload.Segment.InterpSpeed;
                    RaisePropertyChanged(nameof(DefaultInterpSpeed));
                    break;
                case nameof(DispenseSegment.SafeHeight):
                    _step.DispenseDetail.DefaultSafeHeight = payload.Segment.SafeHeight;
                    RaisePropertyChanged(nameof(DefaultSafeHeight));
                    break;
                case nameof(DispenseSegment.ApproachHeight):
                    _step.DispenseDetail.DefaultApproachHeight = payload.Segment.ApproachHeight;
                    RaisePropertyChanged(nameof(DefaultApproachHeight));
                    break;
                case nameof(DispenseSegment.CornerDecel):
                    _step.DispenseDetail.DefaultCornerDecel = payload.Segment.CornerDecel;
                    RaisePropertyChanged(nameof(DefaultCornerDecel));
                    break;
                case nameof(DispenseSegment.DispenseAmount):
                    _step.DispenseDetail.DefaultDispenseAmount = payload.Segment.DispenseAmount;
                    RaisePropertyChanged(nameof(DefaultDispenseAmount));
                    break;
                case nameof(DispenseSegment.DispenseTime):
                    _step.DispenseDetail.DefaultDispenseTime = payload.Segment.DispenseTime;
                    RaisePropertyChanged(nameof(DefaultDispenseTime));
                    break;
                case nameof(DispenseSegment.PostDelay):
                    _step.DispenseDetail.DefaultPostDelay = payload.Segment.PostDelay;
                    RaisePropertyChanged(nameof(DefaultPostDelay));
                    break;
                case nameof(DispenseSegment.DispensingPressure):
                    _step.DispenseDetail.DefaultDispensingPressure = payload.Segment.DispensingPressure;
                    RaisePropertyChanged(nameof(DefaultDispensingPressure));
                    break;
                case nameof(DispenseSegment.SuckBackTime):
                    _step.DispenseDetail.DefaultSuckBackTime = payload.Segment.SuckBackTime;
                    RaisePropertyChanged(nameof(DefaultSuckBackTime));
                    break;
                case nameof(DispenseSegment.GlueTriggerOffsetMm):
                    _step.DispenseDetail.DefaultGlueTriggerOffsetMm = payload.Segment.GlueTriggerOffsetMm;
                    RaisePropertyChanged(nameof(DefaultGlueTriggerOffsetMm));
                    break;
                case nameof(DispenseSegment.PreDelay):
                    _step.DispenseDetail.DefaultPreDelay = payload.Segment.PreDelay;
                    _step.DispenseDetail.DefaultPreDispenseDelay = payload.Segment.PreDelay;
                    RaisePropertyChanged(nameof(DefaultPreDelay));
                    RaisePropertyChanged(nameof(DefaultPreDispenseDelay));
                    break;
                case nameof(DispenseSegment.TeachHeight):
                    _step.DispenseDetail.DefaultTeachHeight = payload.Segment.TeachHeight;
                    RaisePropertyChanged(nameof(DefaultTeachHeight));
                    break;
                case nameof(DispenseSegment.HeightCompensation):
                    _step.DispenseDetail.DefaultHeightCompensation = payload.Segment.HeightCompensation;
                    RaisePropertyChanged(nameof(DefaultHeightCompensation));
                    break;
            }

            // UI 绑定 Effective* 属性，段参数变更后必须刷新显示
            RaiseAllEffectiveParamsChanged();
        }

        /// <summary>
        /// 将源段参数变更同步到对应的 DispenseSegmentRef Override 字段
        /// </summary>
        private void SyncSegmentRefOverride(DispenseSegment seg, string propertyName)
        {
            var segRef = SegmentRefs?.FirstOrDefault(r => r.SourceSegmentId == seg.SegmentId);
            if (segRef == null) return;

            switch (propertyName)
            {
                case nameof(DispenseSegment.JumpSpeed):
                case nameof(DispenseSegment.MoveSpeed):
                    segRef.OverrideMoveSpeed = seg.MoveSpeed;
                    segRef.OverrideJumpSpeed = seg.MoveSpeed;
                    break;
                case nameof(DispenseSegment.InterpSpeed): segRef.OverrideInterpSpeed = seg.InterpSpeed; break;
                case nameof(DispenseSegment.SafeHeight): segRef.OverrideSafeHeight = seg.SafeHeight; break;
                case nameof(DispenseSegment.ApproachHeight): segRef.OverrideApproachHeight = seg.ApproachHeight; break;
                case nameof(DispenseSegment.CornerDecel): segRef.OverrideCornerDecel = seg.CornerDecel; break;
                case nameof(DispenseSegment.DispenseAmount): segRef.OverrideDispenseAmount = seg.DispenseAmount; break;
                case nameof(DispenseSegment.DispenseTime): segRef.OverrideDispenseTime = seg.DispenseTime; break;
                case nameof(DispenseSegment.PreDelay): segRef.OverridePreDelay = seg.PreDelay; break;
                case nameof(DispenseSegment.PostDelay): segRef.OverridePostDelay = seg.PostDelay; break;
                case nameof(DispenseSegment.DispensingPressure): segRef.OverrideDispensingPressure = seg.DispensingPressure; break;
                case nameof(DispenseSegment.SuckBackTime): segRef.OverrideSuckBackTime = seg.SuckBackTime; break;
                case nameof(DispenseSegment.GlueTriggerOffsetMm): segRef.OverrideGlueTriggerOffsetMm = seg.GlueTriggerOffsetMm; break;
                case nameof(DispenseSegment.TeachHeight): segRef.OverrideTeachHeight = seg.TeachHeight; break;
                case nameof(DispenseSegment.HeightCompensation): segRef.OverrideHeightCompensation = seg.HeightCompensation; break;
            }

            if (segRef.UseDefaultParams)
                segRef.UseDefaultParams = false;
        }

        #endregion

        #region 选中段同步——DispenseDetailView 参数跟随选中段

        /// <summary>
        /// 响应选中段变更——将选中段的参数同步到 DispenseDetail 默认参数
        /// </summary>
        private void OnSelectedSegmentChanged(SelectedSegmentPayload payload)
        {
            if (_step?.DispenseDetail == null) return;

            var seg = payload.Segment;
            if (seg == null) return;

            SyncDefaultParamsFromSegment(seg);
            RaiseAllEffectiveParamsChanged();
        }

        /// <summary>
        /// 将源段工艺参数同步到 DispenseDetail 默认参数
        /// </summary>
        private void SyncDefaultParamsFromSegment(DispenseSegment seg)
        {
            if (_step?.DispenseDetail == null || seg == null) return;

            _syncingFromSelection = true;
            try
            {
                _step.DispenseDetail.DefaultMoveSpeed = seg.MoveSpeed;
                _step.DispenseDetail.DefaultJumpSpeed = seg.MoveSpeed;
                _step.DispenseDetail.DefaultInterpSpeed = seg.InterpSpeed;
                _step.DispenseDetail.DefaultSafeHeight = seg.SafeHeight;
                _step.DispenseDetail.DefaultApproachHeight = seg.ApproachHeight;
                _step.DispenseDetail.DefaultCornerDecel = seg.CornerDecel;
                _step.DispenseDetail.DefaultDispenseAmount = seg.DispenseAmount;
                _step.DispenseDetail.DefaultDispenseTime = seg.DispenseTime;
                _step.DispenseDetail.DefaultPreDelay = seg.PreDelay;
                _step.DispenseDetail.DefaultPostDelay = seg.PostDelay;
                _step.DispenseDetail.DefaultDispensingPressure = seg.DispensingPressure;
                _step.DispenseDetail.DefaultSuckBackTime = seg.SuckBackTime;
                _step.DispenseDetail.DefaultGlueTriggerOffsetMm = seg.GlueTriggerOffsetMm;
                _step.DispenseDetail.DefaultPreDispenseDelay = seg.PreDelay;
                _step.DispenseDetail.DefaultTeachHeight = seg.TeachHeight;
                _step.DispenseDetail.DefaultHeightCompensation = seg.HeightCompensation;
            }
            finally
            {
                _syncingFromSelection = false;
            }

            RaisePropertyChanged(nameof(DefaultJumpSpeed));
            RaisePropertyChanged(nameof(DefaultInterpSpeed));
            RaisePropertyChanged(nameof(DefaultMoveSpeed));
            RaisePropertyChanged(nameof(DefaultSafeHeight));
            RaisePropertyChanged(nameof(DefaultApproachHeight));
            RaisePropertyChanged(nameof(DefaultCornerDecel));
            RaisePropertyChanged(nameof(DefaultDispenseAmount));
            RaisePropertyChanged(nameof(DefaultDispenseTime));
            RaisePropertyChanged(nameof(DefaultPreDelay));
            RaisePropertyChanged(nameof(DefaultPostDelay));
            RaisePropertyChanged(nameof(DefaultDispensingPressure));
            RaisePropertyChanged(nameof(DefaultSuckBackTime));
            RaisePropertyChanged(nameof(DefaultGlueTriggerOffsetMm));
            RaisePropertyChanged(nameof(DefaultPreDispenseDelay));
            RaisePropertyChanged(nameof(DefaultTeachHeight));
            RaisePropertyChanged(nameof(DefaultHeightCompensation));
        }

        /// <summary>
        /// 刷新 UI 绑定的 Effective* 工艺参数显示
        /// </summary>
        private void RaiseAllEffectiveParamsChanged()
        {
            RaisePropertyChanged(nameof(EffectiveMoveSpeed));
            RaisePropertyChanged(nameof(EffectiveInterpSpeed));
            RaisePropertyChanged(nameof(EffectiveSafeHeight));
            RaisePropertyChanged(nameof(EffectiveApproachHeight));
            RaisePropertyChanged(nameof(EffectiveCornerDecel));
            RaisePropertyChanged(nameof(EffectiveDispenseTime));
            RaisePropertyChanged(nameof(EffectivePreDelay));
            RaisePropertyChanged(nameof(EffectivePostDelay));
            RaisePropertyChanged(nameof(EffectiveGlueTriggerOffsetMm));
            RaisePropertyChanged(nameof(EffectiveTeachHeight));
            RaisePropertyChanged(nameof(EffectiveHeightCompensation));
        }

        /// <summary>
        /// 从共享存储中的源段刷新当前选中引用的 Override 参数
        /// </summary>
        private void RefreshSelectedRefFromSource()
        {
            if (_selectedSegmentRef == null) return;
            var seg = _dispenseSegmentStore?.CurrentSegments?
                .FirstOrDefault(s => s.SegmentId == _selectedSegmentRef.SourceSegmentId);
            if (seg == null) return;

            _selectedSegmentRef.OverrideMoveSpeed = seg.MoveSpeed;
            _selectedSegmentRef.OverrideJumpSpeed = seg.MoveSpeed;
            _selectedSegmentRef.OverrideInterpSpeed = seg.InterpSpeed;
            _selectedSegmentRef.OverrideSafeHeight = seg.SafeHeight;
            _selectedSegmentRef.OverrideApproachHeight = seg.ApproachHeight;
            _selectedSegmentRef.OverrideCornerDecel = seg.CornerDecel;
            _selectedSegmentRef.OverrideDispenseAmount = seg.DispenseAmount;
            _selectedSegmentRef.OverrideDispenseTime = seg.DispenseTime;
            _selectedSegmentRef.OverridePreDelay = seg.PreDelay;
            _selectedSegmentRef.OverridePostDelay = seg.PostDelay;
            _selectedSegmentRef.OverrideDispensingPressure = seg.DispensingPressure;
            _selectedSegmentRef.OverrideSuckBackTime = seg.SuckBackTime;
            _selectedSegmentRef.OverrideGlueTriggerOffsetMm = seg.GlueTriggerOffsetMm;
            _selectedSegmentRef.OverrideTeachHeight = seg.TeachHeight;
            _selectedSegmentRef.OverrideHeightCompensation = seg.HeightCompensation;
            _selectedSegmentRef.UseDefaultParams = false;
        }

        /// <summary>
        /// 响应 CadPointEditorViewModel.SinglePointProcessParams 变更——同步更新 DispenseDetail 默认参数
        /// </summary>
        private void OnProcessParamsSynced(ProcessParamsSyncPayload payload)
        {
            if (_step?.DispenseDetail == null) return;

            switch (payload.PropertyName)
            {
                case nameof(DotProcessParams.MoveSpeed):
                    _step.DispenseDetail.DefaultMoveSpeed = payload.Value;
                    _step.DispenseDetail.DefaultJumpSpeed = payload.Value;
                    RaisePropertyChanged(nameof(DefaultMoveSpeed));
                    RaisePropertyChanged(nameof(DefaultJumpSpeed));
                    break;
                case nameof(DotProcessParams.SafeHeight):
                    _step.DispenseDetail.DefaultSafeHeight = payload.Value;
                    RaisePropertyChanged(nameof(DefaultSafeHeight));
                    break;
                case nameof(DotProcessParams.ApproachHeight):
                    _step.DispenseDetail.DefaultApproachHeight = payload.Value;
                    RaisePropertyChanged(nameof(DefaultApproachHeight));
                    break;
                case nameof(DotProcessParams.CornerDecel):
                    _step.DispenseDetail.DefaultCornerDecel = payload.Value;
                    RaisePropertyChanged(nameof(DefaultCornerDecel));
                    break;
                case nameof(DotProcessParams.DispenseTime):
                    _step.DispenseDetail.DefaultDispenseTime = payload.Value;
                    RaisePropertyChanged(nameof(DefaultDispenseTime));
                    break;
                case nameof(DotProcessParams.PreDispenseDelay):
                    _step.DispenseDetail.DefaultPreDispenseDelay = payload.Value;
                    _step.DispenseDetail.DefaultPreDelay = payload.Value;
                    RaisePropertyChanged(nameof(DefaultPreDispenseDelay));
                    RaisePropertyChanged(nameof(DefaultPreDelay));
                    break;
                case nameof(DotProcessParams.PostDelay):
                    _step.DispenseDetail.DefaultPostDelay = payload.Value;
                    RaisePropertyChanged(nameof(DefaultPostDelay));
                    break;
                case nameof(DotProcessParams.DotGlueTriggerOffsetMm):
                    _step.DispenseDetail.DefaultGlueTriggerOffsetMm = payload.Value;
                    RaisePropertyChanged(nameof(DefaultGlueTriggerOffsetMm));
                    break;
                case nameof(DotProcessParams.TeachHeight):
                    _step.DispenseDetail.DefaultTeachHeight = payload.Value;
                    RaisePropertyChanged(nameof(DefaultTeachHeight));
                    break;
                case nameof(DotProcessParams.HeightCompensation):
                    _step.DispenseDetail.DefaultHeightCompensation = payload.Value;
                    RaisePropertyChanged(nameof(DefaultHeightCompensation));
                    break;
                case nameof(DotProcessParams.DispensingPressure):
                    _step.DispenseDetail.DefaultDispensingPressure = payload.Value;
                    RaisePropertyChanged(nameof(DefaultDispensingPressure));
                    break;
                case nameof(DotProcessParams.SuckBackTime):
                    _step.DispenseDetail.DefaultSuckBackTime = payload.Value;
                    RaisePropertyChanged(nameof(DefaultSuckBackTime));
                    break;
            }

            RaiseAllEffectiveParamsChanged();
        }

        /// <summary>
        /// 将用户在 DispenseDetailView 修改的参数同步到当前选中段
        /// </summary>
        private void PublishParamToSelectedSegment(string propertyName, double value)
        {
            if (_syncingFromSelection) return;
            var seg = _dispenseSegmentStore?.CurrentSelectedSegment;
            if (seg == null) return;

            switch (propertyName)
            {
                case nameof(DispenseSegment.MoveSpeed): seg.MoveSpeed = value; seg.JumpSpeed = value; break;
                case nameof(DispenseSegment.JumpSpeed): seg.JumpSpeed = value; seg.MoveSpeed = value; break;
                case nameof(DispenseSegment.InterpSpeed): seg.InterpSpeed = value; break;
                case nameof(DispenseSegment.SafeHeight): seg.SafeHeight = value; break;
                case nameof(DispenseSegment.ApproachHeight): seg.ApproachHeight = value; break;
                case nameof(DispenseSegment.CornerDecel): seg.CornerDecel = value; break;
                case nameof(DispenseSegment.DispenseAmount): seg.DispenseAmount = value; break;
                case nameof(DispenseSegment.DispenseTime): seg.DispenseTime = value; break;
                case nameof(DispenseSegment.PreDelay): seg.PreDelay = value; break;
                case nameof(DispenseSegment.PostDelay): seg.PostDelay = value; break;
                case nameof(DispenseSegment.DispensingPressure): seg.DispensingPressure = value; break;
                case nameof(DispenseSegment.SuckBackTime): seg.SuckBackTime = value; break;
                case nameof(DispenseSegment.GlueTriggerOffsetMm): seg.GlueTriggerOffsetMm = value; break;
                case nameof(DispenseSegment.TeachHeight): seg.TeachHeight = value; break;
                case nameof(DispenseSegment.HeightCompensation): seg.HeightCompensation = value; break;
            }
        }

        #endregion

        #region 导入逻辑

        /// <summary>
        /// 获取源分段集合——优先使用共享存储（来自 CAD 编辑器），回退到工站参数
        /// </summary>
        private List<DispenseSegment> GetSourceSegments()
        {
            if (_dispenseSegmentStore?.CurrentSegments != null && _dispenseSegmentStore.CurrentSegments.Count > 0)
                return _dispenseSegmentStore.CurrentSegments.ToList();

            try
            {
                var station = _stationRegistry.GetStation("DispenserStation");
                if (station is IStationParameterProvider provider)
                {
                    var paramsObj = provider.CurrentParameters;
                    if (paramsObj is DispenserStationParams dispenserParams)
                    {
                        return dispenserParams.Segments ?? new List<DispenseSegment>();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"获取点胶工站段数据失败: {ex.Message}");
            }
            return new List<DispenseSegment>();
        }

        /// <summary>
        /// 导入线段类型的源分段到 SegmentRefs
        /// </summary>
        private void OnImportLines()
        {
            ImportSegmentsByType(CadEntityType.Line);
        }

        /// <summary>
        /// 导入弧线/圆类型的源分段到 SegmentRefs
        /// </summary>
        private void OnImportArcs()
        {
            var sources = GetSourceSegments();
            var existingIds = SegmentRefs?.Select(r => r.SourceSegmentId).ToHashSet() ?? new HashSet<string>();

            var candidates = sources.Where(s =>
                (s.EntityType == CadEntityType.Arc || s.EntityType == CadEntityType.Circle)
                && !existingIds.Contains(s.SegmentId)).ToList();

            DispenseSegmentRef lastImported = null;
            DispenseSegment lastSource = null;
            foreach (var seg in candidates)
            {
                var refItem = CreateSegmentRef(seg);
                SegmentRefs?.Add(refItem);
                lastImported = refItem;
                lastSource = seg;
            }

            RefreshSourceSegmentInfo();
            FinalizeImportSync(lastImported, lastSource);
        }

        /// <summary>
        /// 按图元类型导入源分段
        /// </summary>
        private void ImportSegmentsByType(CadEntityType entityType)
        {
            var sources = GetSourceSegments();
            var existingIds = SegmentRefs?.Select(r => r.SourceSegmentId).ToHashSet() ?? new HashSet<string>();

            var candidates = sources.Where(s =>
                s.EntityType == entityType && !existingIds.Contains(s.SegmentId)).ToList();

            DispenseSegmentRef lastImported = null;
            DispenseSegment lastSource = null;
            foreach (var seg in candidates)
            {
                var refItem = CreateSegmentRef(seg);
                SegmentRefs?.Add(refItem);
                lastImported = refItem;
                lastSource = seg;
            }

            RefreshSourceSegmentInfo();
            FinalizeImportSync(lastImported, lastSource);
        }

        /// <summary>
        /// 导入完成后选中最后一段并同步工艺参数到 DISPENSE 面板
        /// </summary>
        private void FinalizeImportSync(DispenseSegmentRef lastImported, DispenseSegment lastSource)
        {
            if (lastImported == null || lastSource == null) return;

            SelectedSegmentRef = lastImported;
            SyncDefaultParamsFromSegment(lastSource);
            RaiseAllEffectiveParamsChanged();
        }

        /// <summary>
        /// 从源分段创建引用对象
        /// </summary>
        private DispenseSegmentRef CreateSegmentRef(DispenseSegment seg)
        {
            return new DispenseSegmentRef
            {
                SourceSegmentId = seg.SegmentId,
                SourceEntityType = seg.EntityType,
                SourceLayerName = seg.LayerName ?? "",
                SourceLength = seg.Length,
                SourcePointCount = seg.Points?.Count ?? 0,
                UseDefaultParams = false,
                OverrideJumpSpeed = seg.MoveSpeed,
                OverrideInterpSpeed = seg.InterpSpeed,
                OverrideMoveSpeed = seg.MoveSpeed,
                OverrideSafeHeight = seg.SafeHeight,
                OverrideApproachHeight = seg.ApproachHeight,
                OverrideDispenseAmount = seg.DispenseAmount,
                OverrideDispenseTime = seg.DispenseTime,
                OverridePreDelay = seg.PreDelay,
                OverridePostDelay = seg.PostDelay,
                OverrideDispensingPressure = seg.DispensingPressure,
                OverrideSuckBackTime = seg.SuckBackTime,
                OverrideGlueTriggerOffsetMm = seg.GlueTriggerOffsetMm,
                OverrideCornerDecel = seg.CornerDecel,
                OverrideTeachHeight = seg.TeachHeight,
                OverrideHeightCompensation = seg.HeightCompensation
            };
        }

        /// <summary>
        /// 刷新分段引用的来源信息，若源段已删除则标记警告
        /// </summary>
        public void RefreshSourceSegmentInfo()
        {
            if (SegmentRefs == null) return;
            var sources = GetSourceSegments();
            var sourceMap = sources.ToDictionary(s => s.SegmentId, s => s);

            foreach (var segRef in SegmentRefs)
            {
                if (sourceMap.TryGetValue(segRef.SourceSegmentId, out var source))
                {
                    segRef.SourceLayerName = source.LayerName ?? "";
                    segRef.SourceLength = source.Length;
                    segRef.SourcePointCount = source.Points?.Count ?? 0;
                }
                else
                {
                    segRef.SourceLayerName = "⚠ 来源已删除";
                    segRef.SourceLength = 0;
                    segRef.SourcePointCount = 0;
                }
            }
        }

        #endregion

        #region 选择操作

        /// <summary>
        /// 移除所有 IsEnabled=true 的分段引用
        /// </summary>
        private void OnRemoveSelected()
        {
            if (SegmentRefs == null) return;
            var toRemove = SegmentRefs.Where(r => r.IsEnabled).ToList();
            foreach (var item in toRemove)
                SegmentRefs.Remove(item);
        }

        /// <summary>
        /// 全选所有分段引用
        /// </summary>
        private void OnSelectAll()
        {
            if (SegmentRefs == null) return;
            foreach (var item in SegmentRefs)
                item.IsEnabled = true;
        }

        /// <summary>
        /// 反选所有分段引用
        /// </summary>
        private void OnInvertSelection()
        {
            if (SegmentRefs == null) return;
            foreach (var item in SegmentRefs)
                item.IsEnabled = !item.IsEnabled;
        }

        #endregion

        #region 关闭与保存

        private void OnClose()
        {
            _eventAggregator?.GetEvent<GlobalVariablesChangedEvent>().Unsubscribe(OnGlobalVariablesChanged);
            try
            {
                var session = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession("MainDialogHost");
                session?.Close(false);
            }
            catch (InvalidOperationException) { }
        }

        private void OnSave()
        {
            OnClose();
        }

        #endregion

        #region INavigationAware

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _step = navigationContext.Parameters.GetValue<ProcessStep>("step");
            if (_step == null) return;

            if (_step.DispenseDetail == null)
                _step.DispenseDetail = new DispenseDetail();

            RefreshSourceSegmentInfo();

            RaisePropertyChanged(nameof(DispenseMode));
            RaisePropertyChanged(nameof(IsDotMode));
            RaisePropertyChanged(nameof(IsArcMode));
            RaisePropertyChanged(nameof(EnableZCalibration));
            RaisePropertyChanged(nameof(ZCompensation3D));
            RaisePropertyChanged(nameof(ZCompensation3DLinkedVar));
            RaisePropertyChanged(nameof(IsZCompensation3DLinked));
            RaisePropertyChanged(nameof(ZCompensationCalibrator));
            RaisePropertyChanged(nameof(ZCompensationCalibratorLinkedVar));
            RaisePropertyChanged(nameof(IsZCompensationCalibratorLinked));
            RaisePropertyChanged(nameof(ManualZCompensation));
            RaisePropertyChanged(nameof(SegmentRefs));
            RaisePropertyChanged(nameof(DefaultJumpSpeed));
            RaisePropertyChanged(nameof(DefaultInterpSpeed));
            RaisePropertyChanged(nameof(DefaultMoveSpeed));
            RaisePropertyChanged(nameof(DefaultSafeHeight));
            RaisePropertyChanged(nameof(DefaultApproachHeight));
            RaisePropertyChanged(nameof(DefaultDispenseAmount));
            RaisePropertyChanged(nameof(DefaultPreDelay));
            RaisePropertyChanged(nameof(DefaultPostDelay));
            RaisePropertyChanged(nameof(DefaultDispensingPressure));
            RaisePropertyChanged(nameof(DefaultSuckBackTime));
            RaisePropertyChanged(nameof(DefaultGlueTriggerOffsetMm));
            RaisePropertyChanged(nameof(DefaultCornerDecel));
            RaisePropertyChanged(nameof(DefaultTeachHeight));
            RaisePropertyChanged(nameof(DefaultHeightCompensation));
            RaisePropertyChanged(nameof(IsDryRunMode));
            RaisePropertyChanged(nameof(IsRealDispenseMode));
            RaisePropertyChanged(nameof(StepDescription));
            RefreshZCompensationDisplayValues();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }

        #endregion

        #region 全局变量加载

        private async Task LoadGlobalVariablesAsync()
        {
            try
            {
                if (_recipePoolService == null) return;

                var poolId = _recipePoolService.CurrentPoolName ?? "Default";
                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);

                AvailableGlobalVariables.Clear();
                foreach (var v in variables)
                    AvailableGlobalVariables.Add(v);

                RefreshZCompensationDisplayValues();
                RaisePropertyChanged(nameof(IsZCompensation3DLinked));
                RaisePropertyChanged(nameof(IsZCompensationCalibratorLinked));
            }
            catch (Exception ex)
            {
                _logger.Warn($"[DispenseDetail] 加载全局变量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 全局变量被外部修改时，刷新 Z 补偿链接变量的实时显示值
        /// </summary>
        private async void OnGlobalVariablesChanged(string poolName)
        {
            try
            {
                if (_recipePoolService == null) return;
                var currentPool = _recipePoolService.CurrentPoolName;
                if (string.IsNullOrEmpty(currentPool) || poolName != currentPool) return;

                var variables = await _recipePoolService.LoadGlobalVariablesAsync(currentPool);
                AvailableGlobalVariables.Clear();
                foreach (var v in variables)
                    AvailableGlobalVariables.Add(v);

                RefreshZCompensationDisplayValues();
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[DispenseDetail] 刷新全局变量显示值失败: {ex.Message}");
            }
        }

        /// <summary>刷新 Z 补偿链接全局变量的实时显示值</summary>
        private void RefreshZCompensationDisplayValues()
        {
            // Z补偿(3D)
            if (!string.IsNullOrEmpty(_step?.DispenseDetail?.ZCompensation3DLinkedVar))
            {
                var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == _step.DispenseDetail.ZCompensation3DLinkedVar);
                ZCompensation3DDisplayValue = gv != null && double.TryParse(gv.Value, out var val) ? val : 0.0;
            }
            else
            {
                ZCompensation3DDisplayValue = ZCompensation3D;
            }
            RaisePropertyChanged(nameof(ZCompensation3DDisplayValue));

            // Z补偿(校准器)
            if (!string.IsNullOrEmpty(_step?.DispenseDetail?.ZCompensationCalibratorLinkedVar))
            {
                var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == _step.DispenseDetail.ZCompensationCalibratorLinkedVar);
                ZCompensationCalibratorDisplayValue = gv != null && double.TryParse(gv.Value, out var val) ? val : 0.0;
            }
            else
            {
                ZCompensationCalibratorDisplayValue = ZCompensationCalibrator;
            }
            RaisePropertyChanged(nameof(ZCompensationCalibratorDisplayValue));
        }

        #endregion
    }
}
