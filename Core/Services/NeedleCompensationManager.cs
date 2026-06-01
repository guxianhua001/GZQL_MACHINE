// NeedleCompensationManager.cs
using Core.Models;
using Prism.Mvvm;
using System;

namespace Core.Services
{
    /// <summary>
    /// 针头 TCP 补偿管理器 — 增量法
    /// ReferenceXYZ 为固定示教基准（永不自动变更）；
    /// TcpTotalOffset 为 X/Y/Z 独立累加的累计 TCP 补偿偏移量。
    /// </summary>
    public class NeedleCompensationManager : BindableBase
    {
        private double _tcpTotalOffsetX;
        private double _tcpTotalOffsetY;
        private double _tcpTotalOffsetZ;

        /// <summary>累计 TCP 补偿 X（相对固定基准的累加偏移）</summary>
        public double TcpTotalOffsetX
        {
            get => _tcpTotalOffsetX;
            set => SetProperty(ref _tcpTotalOffsetX, value);
        }

        /// <summary>累计 TCP 补偿 Y</summary>
        public double TcpTotalOffsetY
        {
            get => _tcpTotalOffsetY;
            set => SetProperty(ref _tcpTotalOffsetY, value);
        }

        /// <summary>累计 TCP 补偿 Z</summary>
        public double TcpTotalOffsetZ
        {
            get => _tcpTotalOffsetZ;
            set => SetProperty(ref _tcpTotalOffsetZ, value);
        }

        /// <summary>兼容旧绑定：等同 TcpTotalOffsetX</summary>
        public double CompensationX
        {
            get => TcpTotalOffsetX;
            set => TcpTotalOffsetX = value;
        }

        /// <summary>兼容旧绑定：等同 TcpTotalOffsetY</summary>
        public double CompensationY
        {
            get => TcpTotalOffsetY;
            set => TcpTotalOffsetY = value;
        }

        /// <summary>兼容旧绑定：等同 TcpTotalOffsetZ</summary>
        public double CompensationZ
        {
            get => TcpTotalOffsetZ;
            set => TcpTotalOffsetZ = value;
        }

        public NeedleCompensationManager()
        {
            ResetTcpTotalOffset();
        }

        /// <summary>
        /// 增量法：本次增量 + 表达式累加到 TcpTotalOffset
        /// </summary>
        public void AccumulateIncremental(double deltaX, double deltaY, double deltaZ,
                                          double exprX, double exprY, double exprZ)
        {
            TcpTotalOffsetX += deltaX + exprX;
            TcpTotalOffsetY += deltaY + exprY;
            TcpTotalOffsetZ += deltaZ + exprZ;
        }

        /// <summary>直接设置累计 TCP 偏移（应用后写入全局变量时使用）</summary>
        public void SetTcpTotalOffset(double x, double y, double z)
        {
            TcpTotalOffsetX = x;
            TcpTotalOffsetY = y;
            TcpTotalOffsetZ = z;
        }

        /// <summary>重置累计 TCP 偏移为零（不改变 ReferenceXYZ）</summary>
        public void ResetTcpTotalOffset()
        {
            TcpTotalOffsetX = 0;
            TcpTotalOffsetY = 0;
            TcpTotalOffsetZ = 0;
        }

        /// <summary>兼容旧 API</summary>
        public void ResetCompensation() => ResetTcpTotalOffset();

        /// <summary>从参数加载累计 TCP 偏移</summary>
        public void LoadFromParameters(NeedleCalibrationParams parameters)
        {
            if (parameters == null) return;

            if (parameters.TcpTotalOffsetX.HasValue) TcpTotalOffsetX = parameters.TcpTotalOffsetX.Value;
            else if (parameters.CompensationStorageX.HasValue) TcpTotalOffsetX = parameters.CompensationStorageX.Value;

            if (parameters.TcpTotalOffsetY.HasValue) TcpTotalOffsetY = parameters.TcpTotalOffsetY.Value;
            else if (parameters.CompensationStorageY.HasValue) TcpTotalOffsetY = parameters.CompensationStorageY.Value;

            if (parameters.TcpTotalOffsetZ.HasValue) TcpTotalOffsetZ = parameters.TcpTotalOffsetZ.Value;
            else if (parameters.CompensationStorageZ.HasValue) TcpTotalOffsetZ = parameters.CompensationStorageZ.Value;
        }

        /// <summary>保存累计 TCP 偏移到参数</summary>
        public void SaveToParameters(NeedleCalibrationParams parameters)
        {
            if (parameters == null) return;

            parameters.TcpTotalOffsetX = TcpTotalOffsetX;
            parameters.TcpTotalOffsetY = TcpTotalOffsetY;
            parameters.TcpTotalOffsetZ = TcpTotalOffsetZ;
            parameters.CompensationStorageX = TcpTotalOffsetX;
            parameters.CompensationStorageY = TcpTotalOffsetY;
            parameters.CompensationStorageZ = TcpTotalOffsetZ;
        }
    }

    /// <summary>
    /// 补偿历史记录 — 增量法
    /// </summary>
    public class CompensationHistoryRecord
    {
        public int SystemNumber { get; set; }
        public DateTime Timestamp { get; set; }
        /// <summary>本次应用的增量补偿</summary>
        public double CompensationX { get; set; }
        public double CompensationY { get; set; }
        public double CompensationZ { get; set; }
        /// <summary>应用后的累计 TCP 偏移</summary>
        public double TcpTotalOffsetX { get; set; }
        public double TcpTotalOffsetY { get; set; }
        public double TcpTotalOffsetZ { get; set; }
        public double CurrentX { get; set; }
        public double CurrentY { get; set; }
        public double CurrentZ { get; set; }
        public double ReferenceX { get; set; }
        public double ReferenceY { get; set; }
        public double ReferenceZ { get; set; }
        public string Operator { get; set; }
        public string Comments { get; set; }
    }

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
