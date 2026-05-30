
namespace Framework.Dialogs
{
    /// <summary>
    /// 通用文件对话框服务接口。
    /// 提供打开/保存文件对话框，返回用户选择的文件路径（取消时返回 null）。
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// 显示“打开文件”对话框。
        /// </summary>
        /// <param name="filter">文件类型过滤器，例如 "JSON files (*.json)|*.json|All files (*.*)|*.*"</param>
        /// <param name="title">对话框标题</param>
        /// <returns>用户选择的文件完整路径，取消则返回 null</returns>
        string ShowOpenFileDialog(string filter = null, string title = null);

        /// <summary>
        /// 显示“保存文件”对话框。
        /// </summary>
        /// <param name="filter">文件类型过滤器</param>
        /// <param name="title">对话框标题</param>
        /// <param name="defaultFileName">默认文件名（不含路径）</param>
        /// <returns>用户保存的文件完整路径，取消则返回 null</returns>
        string ShowSaveFileDialog(string filter = null, string title = null, string defaultFileName = null);
    }
}
