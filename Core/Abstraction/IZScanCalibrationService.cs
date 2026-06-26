namespace Core.Abstraction
{
    /// <summary>
    /// Z-SCAN 标定服务接口，支持双针头（Dz1/Dz2）各自独立的标定状态
    /// </summary>
    public interface IZScanCalibrationService
    {
        double CameraZOffset { get; }
        double NeedleZOffset { get; }
        double TotalZOffset { get; }
        double BaseZ { get; }
        double MeasuredMZ { get; }

        /// <summary> 设置当前活动针头索引（0=Dz1, 1=Dz2） </summary>
        void SetCurrentNeedle(int needleIndex);

        void CalibrateCameraZ(double measuredZ, double referenceZ);
        void ApplyNeedleCompensation(double deltaZ);
        double GetCompensatedZ(double measuredZ);
        void ResetCalibration();
        void SetBaseZ(double baseZ);
        void TeachNeedleMZ(double measuredMZ);

        /// <summary> 从持久化配置恢复当前针头的标定状态（切换针头或加载配置时调用） </summary>
        void RestoreState(double cameraZOffset, double needleZOffset, double baseZ, double measuredMZ);

        /// <param name="baseZ">基准Z高度（与 UI BaseZInput 一致）</param>
        double CalculateDispenseHeight(double baseZ, double baseDispenseHeight, double currentZHeight, double needleCompensation);
        double CalculateZHeightDifference(double baseZ, double currentZHeight);
        event Action CalibrationChanged;
    }
}
