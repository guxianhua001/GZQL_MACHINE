using Prism.Mvvm;

namespace Module.Models
{
    public class WaypointItem : BindableBase
    {
        private int _index;
        public int Index { get => _index; set => SetProperty(ref _index, value); }

        private bool _axisXEnabled;
        public bool AxisXEnabled { get => _axisXEnabled; set => SetProperty(ref _axisXEnabled, value); }

        private double? _axisXPosition;
        public double? AxisXPosition { get => _axisXPosition; set => SetProperty(ref _axisXPosition, value); }

        private bool _axisYEnabled;
        public bool AxisYEnabled { get => _axisYEnabled; set => SetProperty(ref _axisYEnabled, value); }

        private double? _axisYPosition;
        public double? AxisYPosition { get => _axisYPosition; set => SetProperty(ref _axisYPosition, value); }

        private bool _axisUEnabled;
        public bool AxisUEnabled { get => _axisUEnabled; set => SetProperty(ref _axisUEnabled, value); }

        private double? _axisUPosition;
        public double? AxisUPosition { get => _axisUPosition; set => SetProperty(ref _axisUPosition, value); }

        private bool _axisZEnabled;
        public bool AxisZEnabled { get => _axisZEnabled; set => SetProperty(ref _axisZEnabled, value); }

        private double? _axisZPosition;
        public double? AxisZPosition { get => _axisZPosition; set => SetProperty(ref _axisZPosition, value); }

        private double? _dwellTime;
        public double? DwellTime { get => _dwellTime; set => SetProperty(ref _dwellTime, value); }
    }
}
