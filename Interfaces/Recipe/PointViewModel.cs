using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;
using Newtonsoft.Json;

namespace Interfaces
{
    public class PointViewModel : BindableBase
    {
        // 真实坐标（只读）
        public double OriginalX { get; set; }
        public double OriginalY { get; set; }

        private double _x;
        public double X
        {
            get => _x;
            set => SetProperty(ref _x, Math.Round(value, 3)); // 保留3位小数
        }

        private double _y;
        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, Math.Round(value, 3));
        }
        public int _index;
        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }
        private int _row;
        public int Row
        {
            get => _row;
            set => SetProperty(ref _row, value);
        }
        private int _column;
        public int Column
        {
            get => _column;
            set => SetProperty(ref _column, value);
        }
        public int _arrayIndex;
        public int ArrayIndex
        {
            get => _arrayIndex;
            set => SetProperty(ref _arrayIndex, value);
        }
        // 物料二维码
        private string _materialQRCode;
        public string MaterialQRCode
        {
            get => _materialQRCode;
            set => SetProperty(ref _materialQRCode, value);
        }

        //是否检测完成
        private bool _isChecked = false;
        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }
        //检测状态
        private bool? _isOk;
        public bool? IsOk
        {
            get => _isOk;
            set => SetProperty(ref _isOk, value);
        }

        //是否启用
        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }
        // 排序依据
        private int _order;
        public int Order
        {
            get => _order;
            set => SetProperty(ref _order, value);
        }
        //是否旋转角度
        private bool _isRotate;
        public bool IsRotate
        {
            get => _isRotate;
            set => SetProperty(ref _isRotate, value);
        }
        private double _XOffset = 0;
        public double XOffset
        {
            get => _XOffset;
            set => SetProperty(ref _XOffset, Math.Round(value, 3));
        }
        private double _YOffset = 0;
        public double YOffset
        {
            get => _YOffset;
            set => SetProperty(ref _YOffset, Math.Round(value, 3));
        }
        public bool IsCameraMark { get; set; }
        public bool IsEncoderPoint { get; set; }

        // 添加坐标校验逻辑
        public bool Validate() => X >= 0 && Y >= 0;
        // 自动生成的显示坐标（无需保存）
        [JsonIgnore]
        public double DisplayX { get; set; }

        [JsonIgnore]
        public double DisplayY { get; set; }
        // 新增记录字段
        public DialRecord NegativeRecord { get; set; } = new DialRecord();
        public DialRecord PositiveRecord { get; set; } = new DialRecord();
        public DateTime OperationTime { get; set; }
    }
    [Serializable]
    // PointArray模型
    public class PointArray : BindableBase
    {
        public PointArray() { }

        public double XOffset { get; set; }
        public double YOffset { get; set; }
        public ObservableCollection<ObservableCollection<PointOffset>> Points { get; set; } = new();
    }

    // 新增拨针记录类
    public class DialRecord
    {
        public int Sequence { get; set; }                // 拨针序号
        public string NeedleId { get; set; }             // 针编号
        public DateTime OperationTime { get; set; }      // 操作时间
        public string Direction { get; set; }            // 拨针方向（正向或反向）
        public double SearchPosition { get; set; }       // 寻针起始位置
        public double HomeDialForce { get; set; }        // 接触力（寻针时接触力）
        public double HomeDisplacement { get; set; }     // 实际移动距离（寻针时实际移动距离）
        public double HomeTargetPosition { get; set; }   // 目标位置（寻针时目标位置）
        public double HomeActualPosition { get; set; }   // 实际停止位置（寻针时实际停止位置）
        public bool IsHomeSuccess { get; set; }          // 是否成功
        public double DialForce { get; set; }            // 达到的拨针力
        public double DialDisplacement { get; set; }     // 拨针移动距离
        public double TargetPosition { get; set; }       // 目标位置（拨针时目标位置）
        public double ActualPosition { get; set; }       // 实际停止位置（拨针时实际停止位置）
        public bool IsSuccess { get; set; }              // 是否成功
        public double DialHeight { get; set; }           // 拨针高度(mm)
        public int DialCount { get; set; } = 0;          // 当前针的累计拨动次数
        public string ErrorCode { get; set; } = string.Empty; // 错误代码（成功时为Empty）
        // ---- 辅助方法 ----
        // 将方向枚举映射为可读字符串
        public string DisplayDirection =>
                       !string.IsNullOrWhiteSpace(Direction) ? Direction :
                       !string.IsNullOrWhiteSpace(OperationDirection) ? OperationDirection :
                       "UNKNOWN";
        public string OperationDirection { get; set; }
        public override string ToString()
        {
            return $"[针号] {NeedleId}\n" +
                   $"[序号] #{Sequence}\n" +
                   $"[时间] {OperationTime:yyyy-MM-dd HH:mm:ss}\n" +
                   $"[方向] {DisplayDirection}\n" +
                   $"[状态] {(IsSuccess ? "成功" : "失败" + (string.IsNullOrEmpty(ErrorCode) ? "" : $" ({ErrorCode})"))}\n" +
                   $"[高度] {DialHeight:N2}mm\n" +
                   $"[接触力] {HomeDialForce:N2}N\n" +
                   $"[拨针力] {DialForce:N2}N\n" +
                   $"[移动距离] {DialDisplacement:N2}mm\n" +
                   $"[目标位置] {TargetPosition:N2}\n" +
                   $"[实际位置] {ActualPosition:N2}\n" +
                   $"[累计次数] {DialCount}次";
        }
        //public override string ToString() =>
        //    $"[{OperationTime:HH:mm:ss.fff}] {NeedleId} #{Sequence} " +
        //    $"{(IsSuccess ? "OK" : "NG")} (F1={HomeDialForce:N2}N, Δ1={HomeDisplacement:N2}mm,F2={DialForce:N2}N, Δ2={DialDisplacement:N2}mm)";
    }
}
