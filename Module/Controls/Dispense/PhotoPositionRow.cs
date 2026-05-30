using MotionControl.Interfaces;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Module.ViewModels
{
    public enum DispenseType
    {
        Dot,
        Arc
    }

    public enum RunMode
    {
        DryRun,
        Dispense
    }

    /// <summary>
    /// 工作流步骤枚举，用于标识当前操作所处的阶段
    /// </summary>
    public enum WorkflowStep
    {
        Step1_ConfigCapture = 1,
        Step2_PreviewDispense = 2
    }

    /// <summary>
    /// 步骤状态枚举，用于标识每个步骤的视觉状态
    /// </summary>
    public enum StepState
    {
        Pending,
        Active,
        Done
    }

    public class VisionCaptureResult : BindableBase
    {
        private string _rawResponse;
        public string RawResponse
        {
            get => _rawResponse;
            set => SetProperty(ref _rawResponse, value);
        }

        private ObservableCollection<KeyValuePair<string, double>> _parsedData = new ObservableCollection<KeyValuePair<string, double>>();
        public ObservableCollection<KeyValuePair<string, double>> ParsedData
        {
            get => _parsedData;
            set => SetProperty(ref _parsedData, value);
        }

        private ObservableCollection<MachinePointItem> _machinePoints = new ObservableCollection<MachinePointItem>();
        public ObservableCollection<MachinePointItem> MachinePoints
        {
            get => _machinePoints;
            set => SetProperty(ref _machinePoints, value);
        }
    }

    public class MachinePointItem : BindableBase
    {
        private int _index;
        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        private double _x;
        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        private double _y;
        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }
    }

    public class PhotoPositionRow : BindableBase
    {
        public string SiteFeatureName { get; }

        private ObservableCollection<string> _availablePositions = new ObservableCollection<string>();
        public ObservableCollection<string> AvailablePositions
        {
            get => _availablePositions;
            set => SetProperty(ref _availablePositions, value);
        }

        private string _dxPositionName;
        public string DxPositionName
        {
            get => _dxPositionName;
            set => SetProperty(ref _dxPositionName, value);
        }

        private string _dyPositionName;
        public string DyPositionName
        {
            get => _dyPositionName;
            set => SetProperty(ref _dyPositionName, value);
        }

        private string _dz1PositionName;
        public string Dz1PositionName
        {
            get => _dz1PositionName;
            set => SetProperty(ref _dz1PositionName, value);
        }

        private string _yPositionName;
        public string YPositionName
        {
            get => _yPositionName;
            set => SetProperty(ref _yPositionName, value);
        }

        private double _speed = 10.0;
        public double Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        private string _triggerCommand = "TRIGGER";
        public string TriggerCommand
        {
            get => _triggerCommand;
            set => SetProperty(ref _triggerCommand, value);
        }

        private string _connectionName;
        public string ConnectionName
        {
            get => _connectionName;
            set => SetProperty(ref _connectionName, value);
        }

        private int _timeout = 5000;
        public int Timeout
        {
            get => _timeout;
            set => SetProperty(ref _timeout, value);
        }

        private DispenseType _dispenseType = DispenseType.Dot;
        public DispenseType DispenseType
        {
            get => _dispenseType;
            set => SetProperty(ref _dispenseType, value);
        }

        private int _arcSegments = 20;
        public int ArcSegments
        {
            get => _arcSegments;
            set => SetProperty(ref _arcSegments, value);
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isExecuting;
        public bool IsExecuting
        {
            get => _isExecuting;
            set => SetProperty(ref _isExecuting, value);
        }

        private bool _returnToSafeAfterCapture = true;
        /// <summary>
        /// 拍照完成后是否返回安全位
        /// </summary>
        public bool ReturnToSafeAfterCapture
        {
            get => _returnToSafeAfterCapture;
            set => SetProperty(ref _returnToSafeAfterCapture, value);
        }

        private double _needleOffsetX;
        /// <summary>
        /// 针头X偏移基础值
        /// </summary>
        public double NeedleOffsetX
        {
            get => _needleOffsetX;
            set
            {
                if (SetProperty(ref _needleOffsetX, value))
                    RaisePropertyChanged(nameof(CalculatedOffsetX));
            }
        }

        private double _needleOffsetY;
        /// <summary>
        /// 针头Y偏移基础值
        /// </summary>
        public double NeedleOffsetY
        {
            get => _needleOffsetY;
            set
            {
                if (SetProperty(ref _needleOffsetY, value))
                    RaisePropertyChanged(nameof(CalculatedOffsetY));
            }
        }

        private string _offsetXExpression;
        /// <summary>
        /// OffsetX计算表达式，如 "0.1+0.2+0.3"，最终值 = NeedleOffsetX + 表达式结果
        /// </summary>
        public string OffsetXExpression
        {
            get => _offsetXExpression;
            set
            {
                if (SetProperty(ref _offsetXExpression, value))
                    RaisePropertyChanged(nameof(CalculatedOffsetX));
            }
        }

        private string _offsetYExpression;
        /// <summary>
        /// OffsetY计算表达式
        /// </summary>
        public string OffsetYExpression
        {
            get => _offsetYExpression;
            set
            {
                if (SetProperty(ref _offsetYExpression, value))
                    RaisePropertyChanged(nameof(CalculatedOffsetY));
            }
        }

        /// <summary>
        /// 计算后的OffsetX = NeedleOffsetX + 表达式结果
        /// </summary>
        public double CalculatedOffsetX => NeedleOffsetX + EvaluateExpression(OffsetXExpression);

        /// <summary>
        /// 计算后的OffsetY = NeedleOffsetY + 表达式结果
        /// </summary>
        public double CalculatedOffsetY => NeedleOffsetY + EvaluateExpression(OffsetYExpression);

        private double _needleCompensationX;
        /// <summary>
        /// 针头X补偿基础值
        /// </summary>
        public double NeedleCompensationX
        {
            get => _needleCompensationX;
            set
            {
                if (SetProperty(ref _needleCompensationX, value))
                    RaisePropertyChanged(nameof(CalculatedCompensationX));
            }
        }

        private double _needleCompensationY;
        /// <summary>
        /// 针头Y补偿基础值
        /// </summary>
        public double NeedleCompensationY
        {
            get => _needleCompensationY;
            set
            {
                if (SetProperty(ref _needleCompensationY, value))
                    RaisePropertyChanged(nameof(CalculatedCompensationY));
            }
        }

        private string _compensationXExpression;
        /// <summary>
        /// CompensationX计算表达式，最终值 = NeedleCompensationX + 表达式结果
        /// </summary>
        public string CompensationXExpression
        {
            get => _compensationXExpression;
            set
            {
                if (SetProperty(ref _compensationXExpression, value))
                    RaisePropertyChanged(nameof(CalculatedCompensationX));
            }
        }

        private string _compensationYExpression;
        /// <summary>
        /// CompensationY计算表达式
        /// </summary>
        public string CompensationYExpression
        {
            get => _compensationYExpression;
            set
            {
                if (SetProperty(ref _compensationYExpression, value))
                    RaisePropertyChanged(nameof(CalculatedCompensationY));
            }
        }

        /// <summary>
        /// 计算后的CompensationX = NeedleCompensationX + 表达式结果
        /// </summary>
        public double CalculatedCompensationX => NeedleCompensationX + EvaluateExpression(CompensationXExpression);

        /// <summary>
        /// 计算后的CompensationY = NeedleCompensationY + 表达式结果
        /// </summary>
        public double CalculatedCompensationY => NeedleCompensationY + EvaluateExpression(CompensationYExpression);

        /// <summary>
        /// 安全计算数学表达式，如 "0.1+0.2+0.3"，失败返回0
        /// </summary>
        private static double EvaluateExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return 0;
            try
            {
                var result = new DataTable().Compute(expression, null);
                return Convert.ToDouble(result);
            }
            catch
            {
                return 0;
            }
        }

        public PhotoPositionRow(string siteFeatureName)
        {
            SiteFeatureName = siteFeatureName;
        }

        /// <summary>
        /// 外部通知计算属性已变更（全局变量值变化时由ViewModel调用）
        /// </summary>
        public void NotifyCalculatedPropertiesChanged()
        {
            RaisePropertyChanged(nameof(CalculatedOffsetX));
            RaisePropertyChanged(nameof(CalculatedOffsetY));
            RaisePropertyChanged(nameof(CalculatedCompensationX));
            RaisePropertyChanged(nameof(CalculatedCompensationY));
        }

        public async Task LoadPositionsAsync(IPositionProvider positionProvider, string stationId)
        {
            if (string.IsNullOrEmpty(stationId)) return;
            try
            {
                var positions = await positionProvider.GetPositionsAsync(stationId);
                var positionNames = new HashSet<string>();
                foreach (var key in positions.Keys)
                {
                    var parts = key.Split('.');
                    if (parts.Length >= 2)
                    {
                        positionNames.Add(parts[0]);
                    }
                }
                AvailablePositions = new ObservableCollection<string>(positionNames.OrderBy(p => p));
            }
            catch
            {
                AvailablePositions = new ObservableCollection<string>();
            }
        }
    }
}
