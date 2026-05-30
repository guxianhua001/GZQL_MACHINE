using Newtonsoft.Json;
using Prism.Mvvm;
using System.Collections.Generic;
using System.ComponentModel;

namespace Core.Models
{
    /// <summary> DASHBOARD 步骤的详情配置（仅 StepType.DASHBOARD 使用） </summary>
    public class DashboardStepDetail
    {
        /// <summary> 数据字段列表（有序） </summary>
        public List<DashboardField> Fields { get; set; } = new List<DashboardField>();
        
        /// <summary> 背景图片路径（相对或绝对） </summary>
        public string ImagePath { get; set; }
        
        /// <summary> 标注元素列表 </summary>
        public List<DashboardAnnotation> Annotations { get; set; } = new List<DashboardAnnotation>();
        
        /// <summary> 是否启用人工确认（默认true，需人工点击确认继续；false时自动执行下一步） </summary>
        public bool RequireManualConfirm { get; set; } = true;
        
        /// <summary> 超时自动确认(ms)，0=需手动点击确认按钮（仅 RequireManualConfirm=true 时有效） </summary>
        public int AutoConfirmTimeout { get; set; } = 0;

        /// <summary> 运行时确认结果：true=确认OK，false=确认NG，null=未确认。不序列化到配方文件 </summary>
        [JsonIgnore]
        public bool? ConfirmResult { get; set; }
    }

    /// <summary> 看板字段类型：数值型（读取值）或条件型（判断是否为true） </summary>
    public enum DashboardFieldType
    {
        /// <summary> 数值型：表达式返回数值，如 @GV:H2 - @GV:Slot实测 </summary>
        Numeric = 0,
        /// <summary> 条件型：表达式返回布尔值，如 @GV:H2 > 10 </summary>
        Condition = 1
    }

    /// <summary> 看板中的单个数据行 </summary>
    public class DashboardField : BindableBase
    {
        private int _seq;
        private string _displayName;
        private DashboardFieldType _fieldType = DashboardFieldType.Numeric;
        private string _formula;
        private string _conditionFormula;
        private string _format = "F3";
        private double _currentValue;
        private bool? _conditionResult;

        public int Seq { get => _seq; set { if (_seq != value) { _seq = value; RaisePropertyChanged(nameof(Seq)); } } }
        public string DisplayName { get => _displayName; set { if (_displayName != value) { _displayName = value; RaisePropertyChanged(nameof(DisplayName)); } } }

        /// <summary> 字段类型：数值型读取值，条件型判断是否为true </summary>
        public DashboardFieldType FieldType
        {
            get => _fieldType;
            set { if (_fieldType != value) { _fieldType = value; RaisePropertyChanged(nameof(FieldType)); RaisePropertyChanged(nameof(DisplayValue)); } }
        }

        /// <summary> 公式：@GV:变量名 引用全局变量，支持 +-*/() 和数字常量 </summary>
        public string Formula { get => _formula; set { if (_formula != value) { _formula = value; RaisePropertyChanged(nameof(Formula)); } } }

        /// <summary> 条件公式（可选），返回 true/false。为空时无条件通过 </summary>
        public string ConditionFormula { get => _conditionFormula; set { if (_conditionFormula != value) { _conditionFormula = value; RaisePropertyChanged(nameof(ConditionFormula)); } } }

        /// <summary> 值格式化字符串（F3=3位小数, F2, N0） </summary>
        public string Format { get => _format; set { if (_format != value) { _format = value; RaisePropertyChanged(nameof(Format)); RaisePropertyChanged(nameof(DisplayValue)); } } }

        [JsonIgnore]
        public double CurrentValue { get => _currentValue; set { if (_currentValue != value) { _currentValue = value; RaisePropertyChanged(nameof(CurrentValue)); RaisePropertyChanged(nameof(DisplayValue)); } } }

        /// <summary> 显示值：数值型显示格式化数字，条件型显示 ✓/✗ </summary>
        [JsonIgnore]
        public string DisplayValue
        {
            get
            {
                if (_fieldType == DashboardFieldType.Condition)
                {
                    if (_conditionResult == null) return "-";
                    return _conditionResult.Value ? "✓ 通过" : "✗ 未通过";
                }
                return _currentValue.ToString(_format);
            }
        }

        [JsonIgnore]
        public bool? ConditionResult
        {
            get => _conditionResult;
            set { if (_conditionResult != value) { _conditionResult = value; RaisePropertyChanged(nameof(ConditionResult)); RaisePropertyChanged(nameof(DisplayValue)); } }
        }
    }

    /// <summary> 标注元素基类 </summary>
    public abstract class DashboardAnnotation : BindableBase
    {
        private double _x, _y;
        private string _text = "";
        private string _color = "#000000";
        private double _fontSize = 12;

        public double X { get => _x; set { if (_x != value) { _x = value; RaisePropertyChanged(nameof(X)); } } }
        public double Y { get => _y; set { if (_y != value) { _y = value; RaisePropertyChanged(nameof(Y)); } } }
        public string Text { get => _text; set { if (_text != value) { _text = value; RaisePropertyChanged(nameof(Text)); } } }
        public string Color { get => _color; set { if (_color != value) { _color = value; RaisePropertyChanged(nameof(Color)); } } }
        public double FontSize { get => _fontSize; set { if (_fontSize != value) { _fontSize = value; RaisePropertyChanged(nameof(FontSize)); } } }
        
        /// <summary> 序列化用类型标识 </summary>
        [JsonProperty("Type")]
        public abstract string AnnotationType { get; }
    }

    public class TextAnnotation : DashboardAnnotation
    {
        [JsonProperty("Type")]
        public override string AnnotationType => "Text";
    }

    public class LineAnnotation : DashboardAnnotation
    {
        private double _x2, _y2;
        private bool _hasArrow;

        public double X2 { get => _x2; set { if (_x2 != value) { _x2 = value; RaisePropertyChanged(nameof(X2)); } } }
        public double Y2 { get => _y2; set { if (_y2 != value) { _y2 = value; RaisePropertyChanged(nameof(Y2)); } } }
        public bool HasArrow { get => _hasArrow; set { if (_hasArrow != value) { _hasArrow = value; RaisePropertyChanged(nameof(HasArrow)); } } }

        [JsonProperty("Type")]
        public override string AnnotationType => "Line";
    }

    public class RectAnnotation : DashboardAnnotation
    {
        private double _width, _height;
        private string _fillColor = "Transparent";

        public double Width { get => _width; set { if (_width != value) { _width = value; RaisePropertyChanged(nameof(Width)); } } }
        public double Height { get => _height; set { if (_height != value) { _height = value; RaisePropertyChanged(nameof(Height)); } } }
        public string FillColor { get => _fillColor; set { if (_fillColor != value) { _fillColor = value; RaisePropertyChanged(nameof(FillColor)); } } }

        [JsonProperty("Type")]
        public override string AnnotationType => "Rect";
    }
}
