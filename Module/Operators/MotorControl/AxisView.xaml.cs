
using Framework.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Framework.Views
{
    /// <summary>
    /// Axis.xaml 的交互逻辑
    /// </summary>
    public partial class AxisView : UserControl
    {
        public AxisView()
        {
            InitializeComponent();
        }

        private void OnJogPositiveButtonPressed(object sender, RoutedEventArgs e)
        {
            if (DataContext is AxisViewModel viewModel)
            {
                viewModel.StartJog(1); // 假设正方向为 1
            }
        }
        private void OnJogPositiveButtonReleased(object sender, RoutedEventArgs e)
        {
            if (DataContext is AxisViewModel viewModel)
            {
                viewModel.ExecuteStop();
            }
        }
        private void OnJogNegativeButtonPressed(object sender, RoutedEventArgs e)
        {
            if (DataContext is AxisViewModel viewModel)
            {
                viewModel.StartJog(0); // 假设负方向为 0
            }
        }
        private void OnJogNegativeButtonReleased(object sender, RoutedEventArgs e)
        {
            if (DataContext is AxisViewModel viewModel)
            {
                viewModel.ExecuteStop();
            }
        }
    }
}
