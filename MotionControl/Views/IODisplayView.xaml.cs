using System.Windows.Controls;
using MotionControl.ViewModels;

namespace MotionControl.Views
{
    /// <summary>
    /// IO 控制与实时显示视图（Code-Behind）
    /// 负责管理页面生命周期，控制定时刷新的启停
    /// </summary>
    public partial class IODisplayView : UserControl
    {
        private IODisplayViewModel _viewModel;

        /// <summary>
        /// 构造函数：初始化组件并订阅生命周期事件
        /// </summary>
        public IODisplayView()
        {
            InitializeComponent();
            
            // 订阅 Loaded 和 Unloaded 事件
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// 页面加载完成时触发
        /// 设置 ViewModel.IsVisible = true → 启动定时刷新
        /// </summary>
        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 获取 ViewModel 实例（由 PRISM 的 ViewModelLocator 自动注入）
            _viewModel = DataContext as IODisplayViewModel;
            
            if (_viewModel != null)
            {
                // 触发启动刷新（Visibility-Based Refresh）
                _viewModel.IsVisible = true;
                
                System.Diagnostics.Debug.WriteLine(
                    "[IODisplayView] ✅ View Loaded - 刷新已启动");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    "[IODisplayView] ⚠️ View Loaded 但 ViewModel 为空");
            }
        }

        /// <summary>
        /// 页面卸载时触发
        /// 设置 ViewModel.IsVisible = false → 停止定时刷新并释放资源
        /// </summary>
        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                // 触发停止刷新
                _viewModel.IsVisible = false;
                
                System.Diagnostics.Debug.WriteLine(
                    "[IODisplayView] ⏹️ View Unloaded - 刷新已停止");
                
                // 清理引用，帮助 GC 回收
                _viewModel = null;
            }
        }
    }
}
