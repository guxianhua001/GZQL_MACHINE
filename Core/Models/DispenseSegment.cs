// Core/Models/DispenseSegment.cs
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Core.Models
{
    /// <summary>
    /// 点胶分段轨迹模型——单段离散化后的走胶路径，携带工艺参数
    /// </summary>
    public class DispenseSegment : BindableBase
    {
        #region 基础标识属性

        private string _segmentId;
        /// <summary>分段唯一标识（如 "ARC_001"、"LINE_003"）</summary>
        public string SegmentId
        {
            get => _segmentId;
            set => SetProperty(ref _segmentId, value);
        }

        private CadEntityType _entityType;
        /// <summary>来源CAD图元类型</summary>
        public CadEntityType EntityType
        {
            get => _entityType;
            set => SetProperty(ref _entityType, value);
        }

        private CadEntity _sourceEntity;
        /// <summary>来源CAD图元引用（应用采样点数后会被替换为CadLwPolyline）</summary>
        [JsonIgnore]
        public CadEntity SourceEntity
        {
            get => _sourceEntity;
            set => SetProperty(ref _sourceEntity, value);
        }

        private CadEntity _originalSourceEntity;
        /// <summary>原始CAD图元引用（首次导入时的原始图元，如CadArc/CadCircle，不会被替换，用于重新离散化恢复原始轨迹）</summary>
        [JsonIgnore]
        public CadEntity OriginalSourceEntity
        {
            get => _originalSourceEntity;
            set => SetProperty(ref _originalSourceEntity, value);
        }

        private OriginalEntityData _originalEntityData;
        /// <summary>原始图元序列化数据（可JSON序列化，用于保存/加载时恢复原始图元形状）</summary>
        public OriginalEntityData OriginalEntityData
        {
            get => _originalEntityData;
            set => SetProperty(ref _originalEntityData, value);
        }

        #endregion

        #region 几何数据

        private List<CadPoint> _points = new List<CadPoint>();
        /// <summary>离散化后的采样点序列（CAD坐标系）</summary>
        public List<CadPoint> Points
        {
            get => _points;
            set => SetProperty(ref _points, value);
        }

        private int _samplePointCount;
        /// <summary>采样点数（0=使用默认pitch离散化，>0=按指定点数重新采样）</summary>
        public int SamplePointCount
        {
            get => _samplePointCount;
            set => SetProperty(ref _samplePointCount, value);
        }

        private ZMapSegmentProfile _zMapProfile = new ZMapSegmentProfile();
        /// <summary>
        /// 本段ZMAP高度提取配置。随 segments JSON 与配方内嵌段数据自动序列化，
        /// 使ROI、像素↔机械标定及Z基准不再依赖全局单文件。
        /// </summary>
        public ZMapSegmentProfile ZMapProfile
        {
            get => _zMapProfile ??= new ZMapSegmentProfile();
            set => SetProperty(ref _zMapProfile, value ?? new ZMapSegmentProfile());
        }

        /// <summary>
        /// 分段总长度（只读计算属性，遍历Points累加相邻点距离）
        /// </summary>
        [JsonIgnore]
        public double Length
        {
            get
            {
                if (Points == null || Points.Count < 2)
                    return 0.0;

                double total = 0.0;
                for (int i = 1; i < Points.Count; i++)
                {
                    double dx = Points[i].X - Points[i - 1].X;
                    double dy = Points[i].Y - Points[i - 1].Y;
                    double dz = Points[i].Z - Points[i - 1].Z;
                    total += Math.Sqrt(dx * dx + dy * dy + dz * dz);
                }
                return total;
            }
        }

        #endregion

        #region 开关控制

        private bool _isEnabled = true;
        /// <summary>是否启用参与走胶（默认 true），同时作为批量操作的选择依据</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private bool _isSelected;
        /// <summary>用户是否选中该轨迹段（用于删除等破坏性操作），默认 false</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set { SetProperty(ref _isSelected, value); }
        }

        #endregion

        #region 来源信息

        private string _layerName;
        /// <summary>来源图层名称</summary>
        public string LayerName
        {
            get => _layerName;
            set => SetProperty(ref _layerName, value);
        }

        #endregion

        #region 工艺参数
        private double _jumpSpeed = 20.0;
        /// <summary>空移速度 mm/s（范围 1~160，段间跳转时的 XY 移动速度）</summary>
        public double JumpSpeed
        {
            get => _jumpSpeed;
            set => SetProperty(ref _jumpSpeed, Math.Clamp(value, 1.0, 160.0));
        }

        private double _interpSpeed = 1.0;
        /// <summary>连续插补速度 mm/s（范围 0.1~160，走胶轨迹插补使用）</summary>
        public double InterpSpeed
        {
            // 兼容旧数据：Step3 面板曾将插补速度误绑到 JumpSpeed
            get => _interpSpeed > 0 ? _interpSpeed : _jumpSpeed;
            set => SetProperty(ref _interpSpeed, Math.Clamp(value, 0.1, 160.0));
        }

        private double _moveSpeed = 10.0;
        /// <summary>运动速度 mm/s（范围 0.1~50）</summary>
        public double MoveSpeed
        {
            get => _moveSpeed;
            set => SetProperty(ref _moveSpeed, Math.Clamp(value, 0.1, 160.0));
        }

        private double _dispenseAmount = 1.0;
        /// <summary>出胶量（相对值，范围 0~100）</summary>
        public double DispenseAmount
        {
            get => _dispenseAmount;
            set => SetProperty(ref _dispenseAmount, Math.Clamp(value, 0.0, 100.0));
        }

        private double _preDelay = 0.0;
        /// <summary>起点开胶延时 ms（范围 0~5000）</summary>
        public double PreDelay
        {
            get => _preDelay;
            set => SetProperty(ref _preDelay, Math.Clamp(value, 0.0, 5000.0));
        }

        private double _postDelay = 50.0;
        /// <summary>终点关胶延时 ms（范围 0~5000，连续插补结束后泄压等待）</summary>
        public double PostDelay
        {
            get => _postDelay;
            set => SetProperty(ref _postDelay, Math.Clamp(value, 0.0, 5000.0));
        }

        private double _earlyCloseGlueDelayMs = 100.0;
        /// <summary>
        /// 提前关胶延时 ms（范围 0~5000，连续插补模式专用）。
        /// 在预估轨迹结束前此时间关阀，运动继续走完剩余路径，再 PostDelay 泄压，补偿胶阀机械滞后。
        /// </summary>
        public double EarlyCloseGlueDelayMs
        {
            get => _earlyCloseGlueDelayMs;
            set => SetProperty(ref _earlyCloseGlueDelayMs, Math.Clamp(value, 0.0, 5000.0));
        }

        private double _cornerDecel = 0.1;
        /// <summary>拐角减速系数（范围 0~1，越小减速越明显）</summary>
        public double CornerDecel
        {
            get => _cornerDecel;
            set => SetProperty(ref _cornerDecel, Math.Clamp(value, 0.0, 1.0));
        }

        private double _zHeight = 0.0;
        /// <summary>点胶工作高度 mm（范围 -200~200）</summary>
        public double ZHeight
        {
            get => _zHeight;
            set => SetProperty(ref _zHeight, Math.Clamp(value, -100.0, 100.0));
        }

        private double _teachHeight = 0.0;
        /// <summary>示教高度 mm（范围 -200~200，示教时自动记录当前Z轴位置）</summary>
        public double TeachHeight
        {
            get => _teachHeight;
            set => SetProperty(ref _teachHeight, Math.Clamp(value, -100.0, 100.0));
        }

        private double _heightCompensation = 0.0;
        /// <summary>高度补偿值 mm（范围 -50~50，换针后补偿或人工手动补偿，最终工作高度 = TeachHeight + HeightCompensation）</summary>
        public double HeightCompensation
        {
            get => _heightCompensation;
            set => SetProperty(ref _heightCompensation, Math.Clamp(value, -50.0, 50.0));
        }

        private double _xyCompensationX = 0.0;
        /// <summary>XY机械坐标统一补偿 X mm（段内所有插补点 MachineX 叠加）</summary>
        public double XyCompensationX
        {
            get => _xyCompensationX;
            set => SetProperty(ref _xyCompensationX, Math.Clamp(value, -50.0, 50.0));
        }

        private double _xyCompensationY = 0.0;
        /// <summary>XY机械坐标统一补偿 Y mm（段内所有插补点 MachineY 叠加）</summary>
        public double XyCompensationY
        {
            get => _xyCompensationY;
            set => SetProperty(ref _xyCompensationY, Math.Clamp(value, -50.0, 50.0));
        }

        /// <summary>有效工作高度（只读，= TeachHeight + HeightCompensation）</summary>
        [JsonIgnore]
        public double EffectiveZHeight => TeachHeight + HeightCompensation;

        private double _safeHeight = -20.0;
        /// <summary>安全抬升高度 mm（范围 0~200，跨段跳转时使用）</summary>
        public double SafeHeight
        {
            get => _safeHeight;
            set => SetProperty(ref _safeHeight, Math.Clamp(value, -50.0, 50.0));
        }

        private double _glueTriggerOffsetMm = -0.5;
        /// <summary>开胶触发距离 mm（范围 0.05~5.0，Z轴下降过程中距目标高度此距离时触发开胶）</summary>
        public double GlueTriggerOffsetMm
        {
            get => _glueTriggerOffsetMm;
            set => SetProperty(ref _glueTriggerOffsetMm, Math.Clamp(value, -5.0, 5.0));
        }

        private double _dispenseTime = 1800.0;
        /// <summary>出胶时间 ms（范围 10~5000，单点模式下控制胶点大小）</summary>
        public double DispenseTime
        {
            get => _dispenseTime;
            set => SetProperty(ref _dispenseTime, Math.Clamp(value, 10.0, 5000.0));
        }

        private double _approachHeight = -3.0;
        /// <summary>逼近高度 mm（范围 0~50，快速下降到此高度后转为慢速逼近）</summary>
        public double ApproachHeight
        {
            get => _approachHeight;
            set => SetProperty(ref _approachHeight, Math.Clamp(value, -5.0, 5.0));
        }

        private double _dispensingPressure = 0.30;
        /// <summary>点胶气压 MPa（范围 0.1~1.0）</summary>
        public double DispensingPressure
        {
            get => _dispensingPressure;
            set => SetProperty(ref _dispensingPressure, Math.Clamp(value, 0.1, 1.0));
        }

        private double _suckBackTime = 100.0;
        /// <summary>回吸时间 ms（范围 10~500，防止滴漏）</summary>
        public double SuckBackTime
        {
            get => _suckBackTime;
            set => SetProperty(ref _suckBackTime, Math.Clamp(value, 10.0, 500.0));
        }

        #endregion

        /// <summary>
        /// 无参构造函数，初始化默认工艺参数
        /// </summary>
        public DispenseSegment()
        {
            Points = new List<CadPoint>();
        }

        /// <summary>
        /// 带基础信息的构造函数
        /// </summary>
        public DispenseSegment(string segmentId, CadEntityType entityType, string layerName = "")
        {
            SegmentId = segmentId;
            EntityType = entityType;
            LayerName = layerName;
            Points = new List<CadPoint>();
        }
    }
}
