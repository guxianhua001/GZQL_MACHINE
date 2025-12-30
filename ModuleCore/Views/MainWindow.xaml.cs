
using HSMS;
using Interfaces;
using ModuleCore.ViewModels;
using Prism.Regions;
using System.Windows;
using System.Windows.Input;

namespace ModuleCore.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(IRegionManager regionManager)
        {
            InitializeComponent();
            RegionManager.SetRegionManager(ContentRegionCore, regionManager);
            rcMin.Width = this.MinWidth;
            rcMin.Height = this.MinHeight;
            rcNormal = new Rect(this.Left, this.Top, this.Width, this.Height);
            rcWorkArea = SystemParameters.WorkArea;
        }

        private void BtnClose(object sender, RoutedEventArgs e)
        {
            WindowClosedQuestion window = new WindowClosedQuestion();
            _ = window.ShowDialog();

            if (window.IsClosing)
            {
                // 获取 ViewModel 并执行关闭操作
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.CloseSecsGemService();
                }
                IMessage.Logger.Info("应用程序正在关闭...");
                // 关闭窗体
                Application.Current.Shutdown();
            }
            else
            {
                // 必不可少
                //e.Cancel = true;
                return;
            }
            ////App.Current.Shutdown();
            //Application.Current.Shutdown();
        }

        private void BtnMin(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        private void LblSecsStatus_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as MainWindowViewModel;
            viewModel?.ReconnectSecsCommand.Execute(null);
        }

        #region 标题栏事件

        private void Border_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            //如果已经最大化了，就不响应标题栏拖拽
            if (this.Left == rcWorkArea.Left && this.Top == rcWorkArea.Top
                    && this.ActualHeight >= SystemParameters.WorkArea.Height
                    && this.ActualWidth >= SystemParameters.WorkArea.Width)
                return;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        //双击标题栏事件
        private void Border_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (this.ActualWidth < SystemParameters.WorkArea.Width)
                {
                    BtnMaximize_Click(null, null);
                }
                else
                {
                    BtnNormal_Click(null, null);
                }
            }
        }

        #endregion 标题栏事件

        // public static RoutedEvent MouseDoubleClick = EventManager.RegisterRoutedEvent("MouseDoubleClick", RoutingStrategy.Bubble, typeof(MouseButtonEventHandler), typeof(RevitCanvas));
        //==============================================================================================================
        // 还原状态下窗口的位置和大小。
        private Rect rcNormal;

        private Rect rcMin;

        // 工作区大小
        private Rect rcWorkArea;

        /// <summary>
        /// 最大化
        /// </summary>
        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.Left == rcWorkArea.Left && this.Top == rcWorkArea.Top
                    && this.ActualHeight >= SystemParameters.WorkArea.Height
                    && this.ActualWidth >= SystemParameters.WorkArea.Width)
                return;

            //最大化 还原 显示切换
            this.btnMaximize.Visibility = Visibility.Collapsed;
            this.btnNormal.Visibility = Visibility.Visible;
            //保存下当前位置与大小
            if (rcNormal.Width < rcWorkArea.Width)
                rcNormal = new Rect(this.Left, this.Top, this.Width, this.Height);
            rcWorkArea = SystemParameters.WorkArea;

            //设置位置
            this.Left = rcWorkArea.Left;
            this.Top = rcWorkArea.Top;
            this.Width = rcWorkArea.Width;
            this.Height = rcWorkArea.Height;
            //最大化时，把最小尺寸调到最大，目的是禁止拖拽调整窗口大小
            this.MinHeight = rcWorkArea.Height;
            this.MinWidth = rcWorkArea.Width;
        }

        /// <summary>
        /// 还原
        /// </summary>
        private void BtnNormal_Click(object sender, RoutedEventArgs e)
        {
            this.MinHeight = rcMin.Height;
            this.MinWidth = rcMin.Width;

            this.Left = rcNormal.Left;
            this.Top = rcNormal.Top;
            this.Width = rcNormal.Width;
            this.Height = rcNormal.Height;

            //最大化 还原 图标 切换
            this.btnMaximize.Visibility = Visibility.Visible;
            this.btnNormal.Visibility = Visibility.Collapsed;
        }

        //窗口拖动到顶端鼠标出界
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.ActualHeight > rcWorkArea.Height || this.ActualWidth > rcWorkArea.Width)
            {
                this.WindowState = System.Windows.WindowState.Normal;
                PretendMaximize();
            }
        }

        /// <summary>
        /// 假装最大化
        /// </summary>
        private void PretendMaximize()
        {
            //最大化 还原 图标 切换
            this.btnMaximize.Visibility = Visibility.Collapsed;
            this.btnNormal.Visibility = Visibility.Visible;
            //保存下当前位置与大小
            //rcNormal = new Rect(this.Left, this.Top, this.Width, this.Height);

            //获取工作区大小
            Rect rc = SystemParameters.WorkArea;

            //设置位置
            this.Left = rc.Left;
            this.Top = rc.Top;
            this.Width = rc.Width - 2;
            this.Height = rc.Height - 2;

            //最大化时，把最小尺寸调到最大，目的是禁止拖拽调整窗口大小
            this.MinHeight = rc.Height;
            this.MinWidth = rc.Width;
        }

        /// <summary>
        /// 暂停
        /// </summary>
        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            //最大化 还原 图标 切换
            this.tbxPause.Text = "继续";
            this.btnPause.Visibility = Visibility.Collapsed;
            this.btnContinue.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 恢复
        /// </summary>
        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            //最大化 还原 图标 切换
            this.tbxPause.Text = "暂停";
            this.btnPause.Visibility = Visibility.Visible;
            this.btnContinue.Visibility = Visibility.Collapsed;
        }
        /// <summary>
        /// 停止
        /// </summary>
        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            //最大化 还原 图标 切换
            this.btnPause.Visibility = Visibility.Visible;
            this.btnContinue.Visibility = Visibility.Collapsed;
        }
        /// <summary>
        /// 初始化
        /// </summary>
        private void BtnInitialize_Click(object sender, RoutedEventArgs e)
        {
            //最大化 还原 图标 切换
            this.btnPause.Visibility = Visibility.Visible;
            this.btnContinue.Visibility = Visibility.Collapsed;
        }

    }
}