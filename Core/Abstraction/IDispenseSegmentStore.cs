using Core.Models;
using System.Collections.ObjectModel;

namespace Core.Abstraction
{
    /// <summary>
    /// 点胶轨迹段共享存储接口——桥接 CadPointEditorViewModel 与 DispenseDetailViewModel
    /// CadPointEditorViewModel 在 Segments 变化时注册到此处，DispenseDetailViewModel 导入时从此处读取
    /// </summary>
    public interface IDispenseSegmentStore
    {
        /// <summary>当前可用的轨迹段集合（来自 CAD 编辑器）</summary>
        ObservableCollection<DispenseSegment> CurrentSegments { get; }

        /// <summary>当前点胶步骤的 DispenseDetail（用于获取默认参数初始化新段）</summary>
        DispenseDetail CurrentDispenseDetail { get; set; }

        /// <summary>当前选中的段（来自 CAD 编辑器选中行，用于 DispenseDetailView 参数同步）</summary>
        DispenseSegment CurrentSelectedSegment { get; set; }

        /// <summary>最后一次加载/保存轨迹段的配置文件路径（来自配方参数）</summary>
        string LastSegmentConfigPath { get; set; }

        /// <summary>注册轨迹段集合引用</summary>
        void RegisterSegments(ObservableCollection<DispenseSegment> segments);

        /// <summary>清除注册的轨迹段引用</summary>
        void ClearSegments();
    }
}
