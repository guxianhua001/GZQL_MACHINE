using System.Windows;
using System.Windows.Controls;
using Module.Models;
using StationTasks.Models;

namespace Module.Views
{
    /// <summary>
    /// 树形节点详情面板模板选择器：根据选中节点类型返回对应的 DataTemplate
    /// 用于 ProcessSequenceEditorView 右侧详情面板的 ContentControl
    /// </summary>
    public class NodeDetailTemplateSelector : DataTemplateSelector
    {
        /// <summary> 任务节点详情模板（TaskItem 选中时使用） </summary>
        public DataTemplate TaskTemplate { get; set; }

        /// <summary> 方法节点详情模板（ProcessMethod 选中时使用） </summary>
        public DataTemplate MethodTemplate { get; set; }

        /// <summary> 动作节点详情模板（ProcessStep 选中时使用） </summary>
        public DataTemplate StepTemplate { get; set; }

        /// <summary>
        /// 根据节点数据类型选择对应的 DataTemplate
        /// </summary>
        /// <param name="item">当前选中节点</param>
        /// <param name="container">容器对象</param>
        /// <returns>匹配的 DataTemplate，未匹配返回 null</returns>
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            switch (item)
            {
                case TaskItem _:
                    return TaskTemplate;
                case ProcessMethod _:
                    return MethodTemplate;
                case ProcessStep _:
                    return StepTemplate;
                default:
                    return null;
            }
        }
    }
}
