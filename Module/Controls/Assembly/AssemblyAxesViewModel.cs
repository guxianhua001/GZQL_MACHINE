using Module.Models;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class AssemblyAxesViewModel : BindableBase
    {
        private double _globalSpeed = 5.0;
        private ObservableCollection<AxisControl> _axes;

        public double GlobalSpeed
        {
            get => _globalSpeed;
            set
            {
                if (SetProperty(ref _globalSpeed, value))
                {
                    foreach (var axis in Axes)
                        axis.Speed = value;
                }
            }
        }

        public ObservableCollection<AxisControl> Axes
        {
            get => _axes;
            set => SetProperty(ref _axes, value);
        }

        public ICommand HomeAllCommand { get; }
        public ICommand EmergencyStopCommand { get; }

        public AssemblyAxesViewModel()
        {
            // 初始化轴列表（X, Y, Z, Rx, Ry, Rz）
            Axes = new ObservableCollection<AxisControl>
            {
                new AxisControl("X", 150.0, GlobalSpeed),
                new AxisControl("Y", 80.0, GlobalSpeed),
                new AxisControl("Z", 0.0, GlobalSpeed),
                new AxisControl("Rx", 0.0, GlobalSpeed),
                new AxisControl("Ry", 0.0, GlobalSpeed),
                new AxisControl("Rz", 0.0, GlobalSpeed)
            };

            HomeAllCommand = new DelegateCommand(OnHomeAll);
            EmergencyStopCommand = new DelegateCommand(OnEmergencyStop);
        }

        private void OnHomeAll()
        {
            foreach (var axis in Axes)
                axis.Position = 0;
        }

        private void OnEmergencyStop()
        {
            System.Windows.MessageBox.Show("Emergency Stop triggered!");
        }
    }

    // AxisControl 类与 DispenserAxesViewModel 中相同，可放在单独文件中
    // 这里省略重复代码，实际使用时可以提取公共类
}