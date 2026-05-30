using System.Windows;
using System.Windows.Controls;
using Module.ViewModels;

namespace Module.Views
{
    /// <summary>
    /// ZScanDetailView.xaml 的交互逻辑
    /// 支持两种使用模式：
    /// 1. 对话框模式（通过 IDialogAware.OnDialogOpened 初始化）
    /// 2. 嵌入模式（通过 Loaded 事件初始化）
    /// </summary>
    public partial class ZScanDetailView : UserControl
    {
        private bool _isInitialized = false;

        public ZScanDetailView()
        {
            InitializeComponent();

            // 嵌入模式初始化：当控件被直接嵌入页面时（非对话框）
            // PRISM AutoWireViewModel 会先设置 DataContext，然后触发 Loaded 事件
            Loaded += OnControlLoaded;
        }

        /// <summary>
        /// 控件加载完成事件处理（嵌入模式初始化入口）
        /// 当 ZScanDetailView 被直接嵌入到其他页面时使用此路径
        /// 对话框模式下由 OnDialogOpened() 负责，此处会跳过避免重复初始化
        /// </summary>
        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) return;

            if (DataContext is ZScanDetailViewModel vm)
            {
                // 标记为已初始化，防止重复调用
                _isInitialized = true;
                
                // 调用 ViewModel 的公共初始化方法
                vm.InitializeForEmbeddedMode();
            }
        }
    }
}
