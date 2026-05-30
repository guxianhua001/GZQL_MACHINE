using Prism.Mvvm;
using System;
using System.Text.Json.Serialization;

namespace Core.Models
{
    /// <summary>
    /// 点胶工艺参数模型——点涂模式下单点出胶的运动、出胶、阀控及高度参数
    /// </summary>
    public class DotProcessParams : BindableBase
    {
        #region 运动参数

        private double _moveSpeed = 20.0;
        /// <summary>空移速度 mm/s（范围 0.1~50，跨点跳转时的快速移动速度）</summary>
        public double MoveSpeed
        {
            get => _moveSpeed;
            set => SetProperty(ref _moveSpeed, Math.Clamp(value, 0.1, 50.0));
        }

        private double _safeHeight = 5.0;
        /// <summary>安全抬升高度 mm（范围 0~200，跨点跳转时使用）</summary>
        public double SafeHeight
        {
            get => _safeHeight;
            set => SetProperty(ref _safeHeight, Math.Clamp(value, 0.0, 200.0));
        }

        private double _approachHeight = 3.0;
        /// <summary>逼近高度 mm（范围 0~50，快速下降到此高度后转为慢速逼近）</summary>
        public double ApproachHeight
        {
            get => _approachHeight;
            set => SetProperty(ref _approachHeight, Math.Clamp(value, 0.0, 50.0));
        }

        private double _cornerDecel = 0.3;
        /// <summary>拐角减速系数（范围 0~1，越小减速越明显）</summary>
        public double CornerDecel
        {
            get => _cornerDecel;
            set => SetProperty(ref _cornerDecel, Math.Clamp(value, 0.0, 1.0));
        }

        #endregion

        #region 出胶参数

        private double _dispenseTime = 180.0;
        /// <summary>出胶时间 ms（范围 10~5000，控制胶点大小）</summary>
        public double DispenseTime
        {
            get => _dispenseTime;
            set => SetProperty(ref _dispenseTime, Math.Clamp(value, 10.0, 5000.0));
        }

        private double _preDispenseDelay = 50.0;
        /// <summary>起点开胶延时 ms（范围 0~5000，到达起点后延迟开胶）</summary>
        public double PreDispenseDelay
        {
            get => _preDispenseDelay;
            set => SetProperty(ref _preDispenseDelay, Math.Clamp(value, 0.0, 5000.0));
        }

        private double _postDelay = 50.0;
        /// <summary>关胶后延时 ms（范围 0~5000，防拉丝）</summary>
        public double PostDelay
        {
            get => _postDelay;
            set => SetProperty(ref _postDelay, Math.Clamp(value, 0.0, 5000.0));
        }

        private double _dotGlueTriggerOffsetMm = 0.5;
        /// <summary>点涂开胶触发距离 mm（范围 0.05~5.0，Z轴慢速下降过程中距目标位此距离时触发开胶）</summary>
        public double DotGlueTriggerOffsetMm
        {
            get => _dotGlueTriggerOffsetMm;
            set => SetProperty(ref _dotGlueTriggerOffsetMm, Math.Clamp(value, 0.05, 5.0));
        }

        #endregion

        #region 阀控参数

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

        #region 高度参数

        private double _teachHeight = 0.0;
        /// <summary>示教高度 mm（范围 -200~200，示教时自动记录当前Z轴位置）</summary>
        public double TeachHeight
        {
            get => _teachHeight;
            set => SetProperty(ref _teachHeight, Math.Clamp(value, -200.0, 200.0));
        }

        private double _heightCompensation = 0.0;
        /// <summary>高度补偿值 mm（范围 -50~50，换针后补偿或人工手动补偿，最终工作高度 = TeachHeight + HeightCompensation）</summary>
        public double HeightCompensation
        {
            get => _heightCompensation;
            set => SetProperty(ref _heightCompensation, Math.Clamp(value, -50.0, 50.0));
        }

        /// <summary>有效工作高度（只读，= TeachHeight + HeightCompensation）</summary>
        [JsonIgnore]
        public double EffectiveZHeight => TeachHeight + HeightCompensation;

        #endregion
    }
}
