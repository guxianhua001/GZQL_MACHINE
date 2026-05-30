using Prism.Services.Dialogs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    public interface IRecipeDialogService
    {
        /// <summary>
        /// 显示配方选择对话框
        /// </summary>
        /// <param name="availableRecipes">可用配方列表</param>
        /// <param name="title">对话框标题</param>
        /// <param name="message">提示信息</param>
        /// <param name="stationName">工站名称（可选）</param>
        /// <returns>选中的配方名称，若取消则返回 null</returns>
        Task<string> ShowRecipeSelectionDialogAsync(List<string> availableRecipes, string title, string message, string stationName = null);

        /// <summary>
        /// 显示确认对话框
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">消息</param>
        /// <param name="confirmButtonText">确认按钮文本（默认“确认”）</param>
        /// <param name="cancelButtonText">取消按钮文本（默认“取消”）</param>
        /// <returns>true 表示确认，false 表示取消</returns>
        Task<string> ShowConfirmationDialogAsync(string title, string message, string[] options);

        /// <summary>
        /// 显示信息提示框
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">消息</param>
        Task ShowAlertAsync(string title, string message);
    }
}
