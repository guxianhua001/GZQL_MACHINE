using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Core.Abstraction;
using ModuleCore.ViewModels;
using ModuleCore.Views;

namespace ModuleCore.Services
{
    /// <summary>
    /// 基础对话框服务：使用 BaseDialogWindow 统一弹出 UserControl 内容
    /// </summary>
    public class BaseDialogService : IBaseDialogService
    {
        private readonly IThemeService _themeService;
        private BaseDialogWindow _currentWindow;

        /// <summary>
        /// 构造函数：注入全局主题服务
        /// </summary>
        /// <param name="themeService">主题服务，用于窗口主题同步</param>
        public BaseDialogService(IThemeService themeService)
        {
            _themeService = themeService;
        }

        /// <summary>
        /// 显示对话框（模态），返回关闭时的结果
        /// </summary>
        /// <param name="content">UserControl 内容</param>
        /// <param name="title">窗口标题</param>
        /// <returns>对话框关闭时的结果对象</returns>
        public Task<object> ShowDialog(UserControl content, string title = null)
        {
            return ShowDialog(content, title, null);
        }

        /// <summary>
        /// 显示对话框（模态），返回关闭时的结果
        /// </summary>
        /// <param name="content">UserControl 内容</param>
        /// <param name="title">窗口标题</param>
        /// <param name="iconKind">标题栏图标（MaterialDesign PackIcon Kind 名称）</param>
        /// <returns>对话框关闭时的结果对象</returns>
        public Task<object> ShowDialog(UserControl content, string title, string iconKind)
        {
            var tcs = new TaskCompletionSource<object>();

            // 创建窗口和 ViewModel
            var window = new BaseDialogWindow();
            var vm = new BaseDialogWindowViewModel
            {
                Title = title ?? string.Empty,
                Content = content,
                IconKind = iconKind
            };

            // 初始化窗口（绑定主题服务、关闭回调）
            window.Initialize(vm, _themeService);

            // 设置 Owner：确保弹窗始终在主窗口上方，避免被遮挡后无法操作
            // 同时使任务栏显示弹窗缩略图，Alt+Tab 可正常切换
            if (Application.Current?.MainWindow != null)
            {
                window.Owner = Application.Current.MainWindow;
            }

            // 如果内容实现了 IDialogCloseable，订阅其关闭请求
            if (content.DataContext is IDialogCloseable closeable)
            {
                closeable.RequestClose += (result) =>
                {
                    window.Dispatcher.Invoke(() =>
                    {
                        window.DialogResult = result != null;
                        window.Close();
                    });
                };
            }

            // 窗口关闭时完成 Task
            window.Closed += (s, e) =>
            {
                _currentWindow = null;
                tcs.TrySetResult(window.DialogResult);
            };

            _currentWindow = window;

            // 模态显示窗口
            window.ShowDialog();

            return tcs.Task;
        }

        /// <summary>
        /// 关闭当前活动对话框
        /// </summary>
        /// <param name="result">返回结果</param>
        public void CloseDialog(object result = null)
        {
            if (_currentWindow != null)
            {
                _currentWindow.Dispatcher.Invoke(() =>
                {
                    _currentWindow.DialogResult = result != null;
                    _currentWindow.Close();
                });
            }
        }
    }
}
