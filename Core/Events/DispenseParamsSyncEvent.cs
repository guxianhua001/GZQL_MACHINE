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

    /// <summary>
    /// 单点模式全局工艺参数变更事件——CadPointEditorViewModel.SinglePointProcessParams 变更时发布，
    /// 通知 DispenseDetailViewModel 同步更新默认参数
    /// </summary>
    public class ProcessParamsSyncEvent : PubSubEvent<ProcessParamsSyncPayload> { }

    /// <summary>
    /// 单点模式工艺参数同步载荷——携带变更属性名和新值
    /// </summary>
    public class ProcessParamsSyncPayload
    {
        public string PropertyName { get; init; }
        public double Value { get; init; }
    }

    /// <summary>
    /// 点胶针头同步事件——Step3 切换针头或打开 DispenseDetail 时发布，同步 NeedleIndex 与 UI 显示
    /// </summary>
    public class DispenseNeedleIndexChangedEvent : PubSubEvent<int> { }
}
