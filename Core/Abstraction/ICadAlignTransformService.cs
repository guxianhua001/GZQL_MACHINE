// Core/Abstraction/ICadAlignTransformService.cs
using Core.Models;

namespace Core.Abstraction
{
    /// <summary>
    /// CAD 对齐坐标变换共享服务接口——
    /// 由 CadAlignmentViewModel 在完成回转中心/偏移/仿射计算后更新快照，
    /// 供 Dispense 等工具订阅变换变更并按产品旋转角度换算坐标。
    /// 架构说明：单向依赖，Core 层定义接口，Module 层实现，避免倒置依赖。
    /// </summary>
    public interface ICadAlignTransformService
    {
        /// <summary>当前 CAD 对齐变换快照（只读，通过 UpdateSnapshot 更新）</summary>
        CadAlignTransformSnapshot CurrentSnapshot { get; }

        /// <summary>
        /// 更新当前变换快照——由 CadAlignmentViewModel 在计算完成或加载配置后调用
        /// </summary>
        /// <param name="snapshot">新的变换快照</param>
        void UpdateSnapshot(CadAlignTransformSnapshot snapshot);
    }
}
