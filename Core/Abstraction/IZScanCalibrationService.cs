namespace Core.Abstraction
{
    public interface IZScanCalibrationService
    {
        double CameraZOffset { get; }
        double NeedleZOffset { get; }
        double TotalZOffset { get; }
        double BaseZ { get; }
        double MeasuredMZ { get; }
        void CalibrateCameraZ(double measuredZ, double referenceZ);
        void ApplyNeedleCompensation(double deltaZ);
        double GetCompensatedZ(double measuredZ);
        void ResetCalibration();
        void SetBaseZ(double baseZ);
        void TeachNeedleMZ(double measuredMZ);
        double CalculateDispenseHeight(double baseDispenseHeight, double currentZHeight, double needleCompensation);
        double CalculateZHeightDifference(double baseZ, double currentZHeight);
        event Action CalibrationChanged;
    }
}
