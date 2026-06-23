using Core.Abstraction;
using Core.Events;
using Core.Models;
using Prism.Events;

namespace Module.Services
{
    /// <summary>
    /// CAD 对齐坐标变换共享服务实现——
    /// 持有当前变换快照，通过 Prism 事件聚合器通知订阅者（如 DispenseDetailViewModel）。
    /// 注册为单例，确保 CadAlignmentViewModel 与 Dispense 工具共享同一份变换数据。
    /// </summary>
    public class CadAlignTransformService : ICadAlignTransformService
    {
        private readonly IEventAggregator _eventAggregator;

        /// <summary>当前变换快照（初始为无效空快照）</summary>
        public CadAlignTransformSnapshot CurrentSnapshot { get; private set; } = new CadAlignTransformSnapshot();

        public CadAlignTransformService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        /// <summary>
        /// 更新当前变换快照并发布变更事件——
        /// 由 CadAlignmentViewModel 在回转中心/偏移/仿射/旋转角计算完成或配置加载后调用
        /// </summary>
        public void UpdateSnapshot(CadAlignTransformSnapshot snapshot)
        {
            CurrentSnapshot = snapshot ?? new CadAlignTransformSnapshot();
            // 发布变更事件，通知 Dispense 等订阅者同步刷新
            _eventAggregator?.GetEvent<CadAlignTransformChangedEvent>().Publish(CurrentSnapshot);
        }
    }
}
