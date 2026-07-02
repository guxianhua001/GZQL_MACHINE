using Prism.Ioc;
using System.Windows;

namespace MainApp.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(IContainerExtension container)
        {

            InitializeComponent();

            LoadMain(container);
        }

        async void LoadMain(IContainerExtension container)
        {

            await System.Threading.Tasks.Task.Delay(2000);

            var main = container.Resolve<ModuleCore.Views.MainWindow>();

            main.Show();

            // 关键修复：显式将真实主窗口注册为 Application.MainWindow。
            // 本类（MainApp.Views.MainWindow）作为启动闪屏，是 WPF 首个创建的窗口，会被自动设为 Application.MainWindow。
            // 若不显式重定向，闪屏 Close 后 Application.MainWindow 仍指向已关闭的闪屏（WPF 的 RemoveWindow 不会清理 _mainWindow），
            // 后续对话框（BaseDialogService.ShowDialog）执行 window.Owner = Application.Current.MainWindow 时，
            // MainWindow 可能回落为新创建的对话框自身，触发 "Cannot set Owner property to itself" 异常。
            // 直接运行 exe（无调试器）时此问题必现，VS 调试因 GC/时序差异暂未暴露。
            Application.Current.MainWindow = main;

            this.Close();
        }
    }
}
