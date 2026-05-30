using Prism.Events;

namespace Core.Events
{
    /// <summary>
    /// 工站参数保存成功事件，参数为编辑的工站标识符。
    /// </summary>
    public class StationParameterSavedEvent : PubSubEvent<string> { }
}
