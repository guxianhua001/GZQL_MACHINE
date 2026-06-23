using Prism.Events;
using Core.Models;

namespace Core.Events
{
    /// <summary>
    /// CAD 对齐坐标变换变更事件——CadAlignmentViewModel 更新快照后发布，
    /// 通知 DispenseDetailViewModel 等订阅者同步刷新旋转后坐标预览。
    /// </summary>
    public class CadAlignTransformChangedEvent : PubSubEvent<CadAlignTransformSnapshot> { }
}
