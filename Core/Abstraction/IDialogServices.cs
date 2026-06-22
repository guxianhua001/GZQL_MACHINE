using System;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Core.Abstraction
{
    /// <summary>
    /// 可关闭对话框接口：ViewModel 实现此接口以请求关闭对话框
    /// </summary>
    public interface IDialogCloseable
    {
        /// <summary>请求关闭对话框时触发，参数为返回结果（null 表示取消）</summary>
        event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框（用于保存前验证）</summary>
        bool CanCloseDialog();
    }

    /// <summary>
    /// 基础对话框服务接口：统一窗口弹出方式，替代 DialogHost
    /// </summary>
    public interface IBaseDialogService
    {
        /// <summary>
        /// 显示对话框（模态），返回关闭时的结果
        /// </summary>
        /// <param name="content">UserControl 内容</param>
        /// <param name="title">窗口标题</param>
        /// <returns>对话框关闭时的结果对象</returns>
        Task<object> ShowDialog(UserControl content, string title = null);

        /// <summary>
        /// 显示对话框（模态），返回关闭时的结果
        /// </summary>
        /// <param name="content">UserControl 内容</param>
        /// <param name="title">窗口标题</param>
        /// <param name="iconKind">标题栏图标（MaterialDesign PackIcon Kind 名称）</param>
        /// <returns>对话框关闭时的结果对象</returns>
        Task<object> ShowDialog(UserControl content, string title, string iconKind);

        /// <summary>
        /// 关闭当前活动对话框
        /// </summary>
        /// <param name="result">返回结果</param>
        void CloseDialog(object result = null);
    }

    /// <summary>
    /// 全局主题服务接口：管理暗色/明亮主题切换
    /// </summary>
    public interface IThemeService
    {
        /// <summary>当前是否为暗色主题</summary>
        bool IsDarkTheme { get; }

        /// <summary>主题变化事件（参数：是否暗色）</summary>
        event Action<bool> ThemeChanged;

        /// <summary>切换主题（暗色↔明亮）</summary>
        void ToggleTheme();

        /// <summary>设置指定主题</summary>
        /// <param name="isDark">true=暗色，false=明亮</param>
        void SetTheme(bool isDark);

        /// <summary>从持久化配置加载主题并应用</summary>
        void LoadThemeFromSettings();
    }
}
