using Core.Abstraction;
using Core.Models;
using Prism.Mvvm;
using System.Collections.ObjectModel;

namespace Core.Services
{
    /// <summary>
    /// 点胶轨迹段共享存储实现——单例，在 DI 容器中注册
    /// CadPointEditorViewModel 注册 Segments 引用，DispenseDetailViewModel 读取
    /// </summary>
    public class DispenseSegmentStore : BindableBase, IDispenseSegmentStore
    {
        private ObservableCollection<DispenseSegment> _currentSegments;

        public ObservableCollection<DispenseSegment> CurrentSegments => _currentSegments;

        public DispenseDetail CurrentDispenseDetail { get; set; }

        public DispenseSegment CurrentSelectedSegment { get; set; }

        private string _lastSegmentConfigPath = string.Empty;

        /// <summary>最后一次加载/保存轨迹段的配置文件路径（来自配方参数）</summary>
        public string LastSegmentConfigPath
        {
            get => _lastSegmentConfigPath;
            set => SetProperty(ref _lastSegmentConfigPath, value);
        }

        public void RegisterSegments(ObservableCollection<DispenseSegment> segments)
        {
            _currentSegments = segments;
        }

        public void ClearSegments()
        {
            _currentSegments = null;
        }
    }
}
