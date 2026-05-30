
using Prism.Mvvm;

namespace Core.Models
{
    /// <summary>
    /// CAD对应点模型
    /// </summary>
    public class CorrespondencePoint : BindableBase
    {
        private string _name;
        private double _cadX, _cadY, _cadZ;
        private double _actualX, _actualY, _actualZ;
        private double _theoreticalX, _theoreticalY, _theoreticalZ; 

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        // CAD 理论坐标
        public double CadX { get => _cadX; set => SetProperty(ref _cadX, value); }
        public double CadY { get => _cadY; set => SetProperty(ref _cadY, value); }
        public double CadZ { get => _cadZ; set => SetProperty(ref _cadZ, value); }

        // 实际拍摄坐标
        public double ActualX { get => _actualX; set => SetProperty(ref _actualX, value); }
        public double ActualY { get => _actualY; set => SetProperty(ref _actualY, value); }
        public double ActualZ { get => _actualZ; set => SetProperty(ref _actualZ, value); }

        // 理论到位坐标（根据CAD坐标和偏差计算得出）
        public double TheoreticalX { get => _theoreticalX; set => SetProperty(ref _theoreticalX, value); }
        public double TheoreticalY { get => _theoreticalY; set => SetProperty(ref _theoreticalY, value); }
        public double TheoreticalZ { get => _theoreticalZ; set => SetProperty(ref _theoreticalZ, value); }

        // 变换后机械坐标（步骤4 先平移后旋转的结果）
        private double _rotatedX, _rotatedY, _rotatedZ;
        public double RotatedX { get => _rotatedX; set => SetProperty(ref _rotatedX, value); }
        public double RotatedY { get => _rotatedY; set => SetProperty(ref _rotatedY, value); }
        public double RotatedZ { get => _rotatedZ; set => SetProperty(ref _rotatedZ, value); }
    }
}