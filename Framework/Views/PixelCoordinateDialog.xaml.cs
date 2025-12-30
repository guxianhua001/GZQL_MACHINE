using System;
using System.Windows;

namespace Framework.Views
{
    public partial class PixelCoordinateDialog : Window
    {
        public PixelCoordinateDialog()
        {
            InitializeComponent();
        }

        // 确定
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // 数据绑定已把文本写进 PixelX/PixelY，这里只做校验
            if (!int.TryParse(PixelX, out int x) || x < 0 ||
                !int.TryParse(PixelY, out int y) || y < 0)
            {
                MessageBox.Show("请输入非负整数！", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;   // 不关闭窗口
            }

            DialogResult = true;   // 关闭窗口并返回 true
        }

        // 取消
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;  // 关闭窗口并返回 false
        }

        // 依赖属性，方便外部直接取坐标
        public string PixelX
        {
            get => (string)GetValue(PixelXProperty);
            set => SetValue(PixelXProperty, value);
        }
        public static readonly DependencyProperty PixelXProperty =
            DependencyProperty.Register("PixelX", typeof(string), typeof(PixelCoordinateDialog), new PropertyMetadata("0"));

        public string PixelY
        {
            get => (string)GetValue(PixelYProperty);
            set => SetValue(PixelYProperty, value);
        }
        public static readonly DependencyProperty PixelYProperty =
            DependencyProperty.Register("PixelY", typeof(string), typeof(PixelCoordinateDialog), new PropertyMetadata("0"));
    }
}