// NeedleCompensationManager.cs
using Core.Models;
using Prism.Mvvm;
using System;
using System.Collections.Generic;

namespace Core.Services
{
    /// <summary>
    /// 针头补偿管理器 - 清零法补偿逻辑
    /// 清零法：校准后计算补偿值，应用时写入控制器并清零
    /// </summary>
    public class NeedleCompensationManager : BindableBase
    {
        private double _compensationX;
        private double _compensationY;
        private double _compensationZ;

        /// <summary>X轴补偿值</summary>
        public double CompensationX
        {
            get => _compensationX;
            set => SetProperty(ref _compensationX, value);
        }

        /// <summary>Y轴补偿值</summary>
        public double CompensationY
        {
            get => _compensationY;
            set => SetProperty(ref _compensationY, value);
        }

        /// <summary>Z轴补偿值</summary>
        public double CompensationZ
        {
            get => _compensationZ;
            set => SetProperty(ref _compensationZ, value);
        }

        public NeedleCompensationManager()
        {
            ResetCompensation();
        }

        /// <summary>
        /// 更新补偿值（清零法：补偿 = 基准 - 当前）
        /// </summary>
        public void UpdateCompensation(double currentX, double currentY, double currentZ,
                                       double referenceX, double referenceY, double referenceZ)
        {
            CompensationX = referenceX - currentX;
            CompensationY = referenceY - currentY;
            CompensationZ = referenceZ - currentZ;
        }

        /// <summary>
        /// 重置所有补偿值为零
        /// </summary>
        public void ResetCompensation()
        {
            CompensationX = 0;
            CompensationY = 0;
            CompensationZ = 0;
        }

        /// <summary>
        /// 从参数加载补偿管理器状态
        /// </summary>
        public void LoadFromParameters(NeedleCalibrationParams parameters)
        {
            if (parameters == null) return;

            if (parameters.CompensationStorageX.HasValue) CompensationX = parameters.CompensationStorageX.Value;
            if (parameters.CompensationStorageY.HasValue) CompensationY = parameters.CompensationStorageY.Value;
            if (parameters.CompensationStorageZ.HasValue) CompensationZ = parameters.CompensationStorageZ.Value;
        }

        /// <summary>
        /// 保存补偿管理器状态到参数
        /// </summary>
        public void SaveToParameters(NeedleCalibrationParams parameters)
        {
            if (parameters == null) return;

            parameters.CompensationStorageX = CompensationX;
            parameters.CompensationStorageY = CompensationY;
            parameters.CompensationStorageZ = CompensationZ;
        }
    }

    /// <summary>
    /// 补偿历史记录 - 清零法简化版
    /// </summary>
    public class CompensationHistoryRecord
    {
        public int SystemNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public double CompensationX { get; set; }
        public double CompensationY { get; set; }
        public double CompensationZ { get; set; }
        public double CurrentX { get; set; }
        public double CurrentY { get; set; }
        public double CurrentZ { get; set; }
        public double ReferenceX { get; set; }
        public double ReferenceY { get; set; }
        public double ReferenceZ { get; set; }
        public string Operator { get; set; }
        public string Comments { get; set; }
    }

    // 事件类
    public class CompensationChangeAlertEventArgs
    {
        public int SystemNumber { get; set; }
        public double DeltaX { get; set; }
        public double DeltaY { get; set; }
        public double DeltaZ { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class CompensationChangeAlertEvent : Prism.Events.PubSubEvent<CompensationChangeAlertEventArgs> { }

}
