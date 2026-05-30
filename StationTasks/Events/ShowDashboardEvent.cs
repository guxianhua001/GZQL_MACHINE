using Prism.Events;
using StationTasks.Models;

namespace StationTasks.Events
{
    /// <summary> DASHBOARD 步骤事件的载荷数据 </summary>
    public class ShowDashboardPayload
    {
        /// <summary> 当前执行的步骤信息 </summary>
        public ProcessStep Step { get; set; }
        /// <summary> 看板数据字段列表 </summary>
        public System.Collections.ObjectModel.ObservableCollection<Core.Models.DashboardField> Fields { get; set; }
        /// <summary> 背景图片路径 </summary>
        public string ImagePath { get; set; }
        /// <summary> 标注元素列表 </summary>
        public System.Collections.ObjectModel.ObservableCollection<Core.Models.DashboardAnnotation> Annotations { get; set; }
        /// <summary> 是否为执行模式（true=运行时弹出，false=编辑器预览） </summary>
        public bool IsExecutionMode { get; set; }
    }

    /// <summary> DASHBOARD 步骤执行时发布，通知 UI 打开数据看板弹窗 </summary>
    public class ShowDashboardEvent : PubSubEvent<ShowDashboardPayload> { }

    /// <summary> 用户确认看板时的结果枚举 </summary>
    public enum DashboardConfirmResult
    {
        /// <summary> 确认继续：流程正常继续 </summary>
        Continue,
        /// <summary> 确认NG：流程判定为不合格 </summary>
        NG
    }

    /// <summary> 用户点击确认后发布，通知 DashboardStepAction 继续流程（携带确认结果） </summary>
    public class DashboardConfirmedEvent : PubSubEvent<DashboardConfirmResult> { }
}
