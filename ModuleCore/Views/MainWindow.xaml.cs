using Core.Abstraction;
using Core.Utilities;
using ModuleCore.ViewModels;
using MotionControl.Events;
using MotionControl.Views;
using MotionControl.ViewModels;
using Prism.Events;
using Prism.Ioc;
using Prism.Regions;
using System.Windows;
using System.Windows.Input;
using System;

namespace ModuleCore.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IEventAggregator _ea;
        private readonly IContainerProvider _container;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        public MainWindow(IRegionManager regionManager, IEventAggregator ea, IContainerProvider container, ILoggerService logger, ILocalizationService localization)
        {
            InitializeComponent();
            RegionManager.SetRegionManager(ContentRegionCore, regionManager);
            rcMin.Width = this.MinWidth;
            rcMin.Height = this.MinHeight;
            rcNormal = new Rect(this.Left, this.Top, this.Width, this.Height);
            rcWorkArea = SystemParameters.WorkArea;

            _ea = ea;
            _container = container;
            _logger = logger;
            _localization = localization;

            // 初始化轴控制面板（通过 Prism 容器解析 ViewModel）
            InitializeAxisControlPanel();
            // 订阅可恢复异常事件
            _ea.GetEvent<RecoverableFaultEvent>().Subscribe(OnRecoverableFault, ThreadOption.PublisherThread, true);
        }

        /// <summary>
        /// 初始化轴控制面板：通过 Prism 容器解析 View+ViewModel
        /// </summary>
        private void InitializeAxisControlPanel()
        {
            try
            {
                var viewModel = _container.Resolve<AxisControlPanelViewModel>();
                var view = new AxisControlPanelView();
                view.DataContext = viewModel;
                AxisPanelContent.Content = view;
                System.Diagnostics.Debug.WriteLine(_localization.GetResourceOrDefault("MW_Log_AxisPanelInitSuccess", "[MainWindow] 轴控制面板初始化成功"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format(_localization.GetResourceOrDefault("MW_Log_AxisPanelInitFailed", "[MainWindow] 轴控制面板初始化失败: {0}\n{1}"), ex.Message, ex.StackTrace));
            }
        }

        private void OnRecoverableFault(RecoverableFaultPayload payload)
        {
            // 必须切回 UI 线程操作 DialogHost
            Application.Current.Dispatcher.Invoke(async () =>
            {
                var dialogView = new RecoverableFaultDialogView();
                if (dialogView.DataContext is MotionControl.ViewModels.RecoverableFaultDialogViewModel vm)
                {
                    // 将异常信息传递给弹窗的 ViewModel
                    vm.TaskId = payload.TaskId;
                    vm.TaskName = payload.TaskName;
                    vm.StepName = payload.StepName;
                    vm.ErrorMessage = payload.ErrorMessage;
                    vm.SuggestedAction = payload.SuggestedAction;
                }

                // 调用 MaterialDesign 的 DialogHost 弹出全局模态窗口
                // "MainDialogHost" 对应你 MainWindow.xaml 中 DialogHost 的 Identifier
                await MaterialDesignThemes.Wpf.DialogHost.Show(dialogView, "MainDialogHost");
            });
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
                _logger.Info(_localization.GetResourceOrDefault("MW_Log_AppClosing", "应用程序正在关闭..."));
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
            //this.tbxPause.Text = "继续";
            //this.btnPause.Visibility = Visibility.Collapsed;
            //this.btnContinue.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void BtnInitialize_Click(object sender, RoutedEventArgs e)
        {
            //最大化 还原 图标 切换
            //this.tbxPause.Text = "暂停";
            //this.btnPause.Visibility = Visibility.Visible;
            //this.btnContinue.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 点击轴控制面板遮罩层时关闭面板
        /// </summary>
        private void AxisPanelOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.IsAxisPanelOpen = false;
            }
        }

        /// <summary>
        /// 停止
        /// </summary>
        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
        }

    }
}