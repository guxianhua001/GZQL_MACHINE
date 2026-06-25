using System;
using System.Globalization;
using Core.Models;
using MotionControl.Interfaces;
using Prism.Mvvm;
using StationTasks.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Module.ViewModels
{
    /// <summary>
    /// SubMove 行级 ViewModel，为每行提供独立的轴列表和位置列表
    /// 解决 DataGrid 多行共享同一数据源导致 ComboBox 选项不正确的问题
    /// </summary>
    public class SubMoveRowViewModel : BindableBase
    {
        private readonly SubMove _subMove;
        private readonly IPositionProvider _positionProvider;
        private ObservableCollection<GlobalVariable> _linkedVariables;

        /// <summary> 底层 SubMove 模型，保存时回写 </summary>
        public SubMove SubMove => _subMove;

        // 转发 SubMove 属性
        public string SubSeq { get => _subMove.SubSeq; set => _subMove.SubSeq = value; }
        public string StationId
        {
            get => _subMove.StationId;
            set
            {
                if (_subMove.StationId != value)
                {
                    _subMove.StationId = value;
                    RaisePropertyChanged(nameof(StationId));
                    if (!string.IsNullOrEmpty(value))
                        LoadAxesAndPositionsAsync(value).ConfigureAwait(false);
                }
            }
        }
        public string Axis { get => _subMove.Axis; set => _subMove.Axis = value; }
        public int AxisId { get => _subMove.AxisId; set => _subMove.AxisId = value; }
        public string PositionName
        {
            get => _subMove.PositionName;
            set
            {
                if (_subMove.PositionName != value)
                {
                    _subMove.PositionName = value;
                    RaisePropertyChanged(nameof(PositionName));
                    RaisePropertyChanged(nameof(IsPositionInvalid));
                }
            }
        }
        public string Description { get => _subMove.Description; set => _subMove.Description = value; }
        public double Offset { get => _subMove.Offset; set => _subMove.Offset = value; }
        /// <summary> 链接变量时显示的实时数值（从全局变量的 Value 解析而来） </summary>
        private double _offsetDisplayValue;
        public double OffsetDisplayValue
        {
            get => _offsetDisplayValue;
            set => SetProperty(ref _offsetDisplayValue, value);
        }

        public string OffsetVariableName
        {
            get => _subMove.OffsetVariableName;
            set
            {
                if (_subMove.OffsetVariableName != value)
                {
                    _subMove.OffsetVariableName = value;
                    RaisePropertyChanged(nameof(OffsetVariableName));
                    RaisePropertyChanged(nameof(IsOffsetLinked));
                    RaisePropertyChanged(nameof(OffsetDisplayText));
                    UpdateOffsetDisplayValue();
                    // 如果输入的是数值，同步到 Offset 字段
                    if (double.TryParse(value, out var numVal))
                        _subMove.Offset = numVal;
                }
            }
        }
        public double Speed { get => _subMove.Speed; set => _subMove.Speed = value; }

        /// <summary> 回零模式数值（0=卡内配置，其他=自定义模式值，如 1/2/3...） </summary>
        public int HomeMode
        {
            get => _subMove.HomeMode;
            set
            {
                if (_subMove.HomeMode != value)
                {
                    _subMove.HomeMode = value;
                    RaisePropertyChanged(nameof(HomeMode));
                    RaisePropertyChanged(nameof(HomeType));
                    RaisePropertyChanged(nameof(IsHomeParamsEditable));
                }
            }
        }

        public double HomeMinVel { get => _subMove.HomeMinVel; set => _subMove.HomeMinVel = value; }
        public double HomeMaxVel { get => _subMove.HomeMaxVel; set => _subMove.HomeMaxVel = value; }

        // 回零类型选项列表（静态共享，所有行共用）
        private static readonly ObservableCollection<string> _homeTypeOptions = new ObservableCollection<string>
        {
            "Card Config",
            "Custom"
        };
        /// <summary> 回零类型下拉选项列表：卡内配置 / 自定义参数 </summary>
        public ObservableCollection<string> HomeTypeOptions => _homeTypeOptions;

        /// <summary> 回零类型显示文本，与 HomeMode 数值双向转换（0=Card Config，其他=Custom） </summary>
        public string HomeType
        {
            get => HomeMode == 0 ? HomeTypeOptions[0] : HomeTypeOptions[1];
            set
            {
                bool isCardConfig = (value == HomeTypeOptions[0]);
                int newMode;
                if (isCardConfig)
                {
                    newMode = 0;
                }
                else
                {
                    // 切换到自定义时，若当前为0则默认设为模式1
                    newMode = HomeMode == 0 ? 1 : HomeMode;
                }
                if (HomeMode != newMode)
                    HomeMode = newMode;
            }
        }

        /// <summary> 回零参数是否可编辑（仅自定义模式下可编辑 Mode/MinVel/MaxVel） </summary>
        public bool IsHomeParamsEditable => HomeMode != 0;

        // Action 属性转发（支持在运动序列中插入夹爪/延时等动作，参数统一使用夹爪配置默认值）
        public SubMoveAction Action
        {
            get => _subMove.Action;
            set
            {
                if (_subMove.Action != value)
                {
                    _subMove.Action = value;
                    RaisePropertyChanged(nameof(Action));
                    RaisePropertyChanged(nameof(IsMotionEnabled));
                }
            }
        }

        /// <summary> 当 Action=None 时允许编辑运动参数，选择其他 Action 后禁用运动相关列 </summary>
        public bool IsMotionEnabled => Action == SubMoveAction.None;

        /// <summary> Offset 是否链接了全局变量（非空且非纯数值时显示链接图标） </summary>
        public bool IsOffsetLinked =>
            !string.IsNullOrEmpty(OffsetVariableName) &&
            !double.TryParse(OffsetVariableName, out _);

        /// <summary> Offset 列显示文本：链接变量时显示变量名，否则显示数值 </summary>
        public string OffsetDisplayText
        {
            get
            {
                if (!string.IsNullOrEmpty(OffsetVariableName) && !double.TryParse(OffsetVariableName, out _))
                    return OffsetVariableName;
                return Offset.ToString("G");
            }
        }

        // 可用的动作类型列表（静态共享实例，所有行共用）
        private static readonly ObservableCollection<SubMoveAction> _availableActions =
            new ObservableCollection<SubMoveAction>(Enum.GetValues(typeof(SubMoveAction)).Cast<SubMoveAction>());
        /// <summary> 可用的动作类型枚举列表，供 ComboBox 绑定使用 </summary>
        public ObservableCollection<SubMoveAction> AvailableActions => _availableActions;

        // 每行独立的轴和位置列表
        private ObservableCollection<string> _availableAxes = new ObservableCollection<string>();
        /// <summary> 当前工站可用的轴名称列表 </summary>
        public ObservableCollection<string> AvailableAxes
        {
            get => _availableAxes;
            set => SetProperty(ref _availableAxes, value);
        }

        private ObservableCollection<string> _availablePositions = new ObservableCollection<string>();
        /// <summary> 当前工站可用的位置名称列表 </summary>
        public ObservableCollection<string> AvailablePositions
        {
            get => _availablePositions;
            set
            {
                if (SetProperty(ref _availablePositions, value))
                    RaisePropertyChanged(nameof(IsPositionInvalid));
            }
        }

        /// <summary> 位置名在当前 AvailablePositions 中不存在（被重命名/删除）时为 true，UI 显示警告图标 </summary>
        public bool IsPositionInvalid =>
            !string.IsNullOrEmpty(PositionName) &&
            AvailablePositions != null &&
            AvailablePositions.Count > 0 &&
            !AvailablePositions.Contains(PositionName);

        public SubMoveRowViewModel(SubMove subMove, IPositionProvider positionProvider)
        {
            _subMove = subMove;
            _positionProvider = positionProvider;
            UpdateOffsetDisplayValue();
        }

        /// <summary>
        /// 根据链接的全局变量值更新 OffsetDisplayValue
        /// </summary>
        public void UpdateOffsetDisplayValue()
        {
            if (!string.IsNullOrEmpty(OffsetVariableName) && !double.TryParse(OffsetVariableName, out _))
            {
                var variable = _linkedVariables?
                    .Cast<GlobalVariable>()
                    .FirstOrDefault(v => string.Equals(v.Name, OffsetVariableName, StringComparison.OrdinalIgnoreCase));
                if (variable != null && double.TryParse(variable.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                    OffsetDisplayValue = val;
            }
        }

        /// <summary> 设置可链接全局变量列表引用（由父 ViewModel 调用） </summary>
        internal void SetLinkableVariables(ObservableCollection<GlobalVariable> variables)
        {
            _linkedVariables = variables;
        }

        /// <summary>
        /// 根据 stationId 异步加载该工站的轴列表和位置列表
        /// </summary>
        public async Task LoadAxesAndPositionsAsync(string stationId)
        {
            if (string.IsNullOrEmpty(stationId)) return;
            try
            {
                var positions = await _positionProvider.GetPositionsAsync(stationId);
                var axes = new HashSet<string>();
                var positionNames = new HashSet<string>();
                foreach (var key in positions.Keys)
                {
                    var parts = key.Split('.');
                    if (parts.Length >= 2)
                    {
                        positionNames.Add(parts[0]);
                        axes.Add(parts[1]);
                    }
                }
                AvailableAxes = new ObservableCollection<string>(axes.OrderBy(a => a));
                AvailablePositions = new ObservableCollection<string>(positionNames.OrderBy(p => p));
            }
            catch
            {
                AvailableAxes = new ObservableCollection<string>();
                AvailablePositions = new ObservableCollection<string>();
            }
        }
    }
}
