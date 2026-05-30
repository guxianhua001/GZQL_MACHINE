using Prism.Events;

namespace Core.Events
{
    /// <summary>
    /// 段参数变更事件——Step3EditParamsPanel 修改段参数时发布，
    /// 通知 DispenseDetailViewModel 反向同步默认参数
    /// </summary>
    public class SegmentParamChangedEvent : PubSubEvent<SegmentParamPayload> { }

    /// <summary>
    /// 段参数变更载荷——携带变更属性名和段引用
    /// </summary>
    public class SegmentParamPayload
    {
        public string PropertyName { get; init; }
        public Core.Models.DispenseSegment Segment { get; init; }
    }

    /// <summary>
    /// 选中段变更事件——Step3EditParamsPanel 选中行切换时发布，
    /// 通知 DispenseDetailViewModel 将默认参数更新为选中段的参数
    /// </summary>
    public class SelectedSegmentChangedEvent : PubSubEvent<SelectedSegmentPayload> { }

    /// <summary>
    /// 选中段变更载荷——携带新选中的段引用（null 表示无选中）
    /// </summary>
    public class SelectedSegmentPayload
    {
        public Core.Models.DispenseSegment Segment { get; init; }
    }
}
