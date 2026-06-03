using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Prism.Commands;
using Prism.Mvvm;

namespace Framework.Models
{
    /// <summary>
    /// 自定义弹窗按钮数据模型
    /// 每个按钮可独立配置文本、背景色、图标、点击回调
    /// </summary>
    public class DialogButton : BindableBase
    {
        private string _text;
        /// <summary>按钮文本</summary>
        public string Text
        {
            get => _text;
            set { _text = value; RaisePropertyChanged(nameof(Text)); }
        }

        private string _backgroundHex = "#757575";
        /// <summary>按钮背景色（十六进制字符串，如 "#4CAF50"）</summary>
        public string BackgroundHex
        {
            get => _backgroundHex;
            set
            {
                _backgroundHex = value;
                RaisePropertyChanged(nameof(BackgroundHex));
                RaisePropertyChanged(nameof(BackgroundBrush));
            }
        }

        /// <summary>背景色 Brush（由 BackgroundHex 自动转换）</summary>
        public Brush BackgroundBrush
        {
            get
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(BackgroundHex);
                    return new SolidColorBrush(color);
                }
                catch { return new SolidColorBrush(Colors.Gray); }
            }
        }

        private PackIconKind _iconKind = PackIconKind.None;
        /// <summary>按钮图标</summary>
        public PackIconKind IconKind
        {
            get => _iconKind;
            set { _iconKind = value; RaisePropertyChanged(nameof(IconKind)); }
        }

        private int _buttonIndex;
        /// <summary>按钮索引，用于返回结果</summary>
        public int ButtonIndex
        {
            get => _buttonIndex;
            set { _buttonIndex = value; RaisePropertyChanged(nameof(ButtonIndex)); }
        }

        /// <summary>点击命令，由 ViewModel 注入</summary>
        public DelegateCommand ClickCommand { get; set; }
    }
}
