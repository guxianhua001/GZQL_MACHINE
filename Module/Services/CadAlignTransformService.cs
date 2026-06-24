using Core.Abstraction;
using Core.Events;
using Core.Models;
using Core.Utilities;
using Prism.Events;
using Recipe.Events;
using Recipe.Interfaces;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Module.Services
{
    /// <summary>
    /// CAD 对齐坐标变换共享服务实现——
    /// 持有当前变换快照，通过 Prism 事件聚合器通知订阅者（如 DispenseDetailViewModel）。
    /// 注册为单例，确保 CadAlignmentViewModel 与 Dispense 工具共享同一份变换数据。
    /// 启动时及配方池切换时自动从持久化配置恢复快照，避免重启后 View Rotation Coords 不可用。
    /// </summary>
    public class CadAlignTransformService : ICadAlignTransformService
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;

        /// <summary>当前变换快照（初始为无效空快照）</summary>
        public CadAlignTransformSnapshot CurrentSnapshot { get; private set; } = new CadAlignTransformSnapshot();

        public CadAlignTransformService(
            IEventAggregator eventAggregator,
            IRecipePoolService recipePoolService,
            ILoggerService logger)
        {
            _eventAggregator = eventAggregator;
            _recipePoolService = recipePoolService;
            _logger = logger;

            // 配方池切换时重新加载 CAD 对齐变换快照
            _eventAggregator?.GetEvent<RecipePoolChangedEvent>().Subscribe(
                poolName => { _ = TryRestoreSnapshotAsync(); },
                ThreadOption.BackgroundThread);

            // 配方池名称变更时（含启动后 LoadPoolsAsync 设置默认池）同步恢复
            if (_recipePoolService is INotifyPropertyChanged npc)
                npc.PropertyChanged += OnRecipePoolPropertyChanged;

            // 启动时从持久化配置恢复（不依赖用户先打开 CAD Alignment 页面）
            _ = TryRestoreSnapshotAsync();
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

        /// <inheritdoc />
        public async Task EnsureSnapshotRestoredAsync()
        {
            if (CurrentSnapshot?.IsValid == true)
                return;

            await TryRestoreSnapshotAsync().ConfigureAwait(false);
        }

        /// <summary>配方池 CurrentPoolName 变更时重新恢复变换快照</summary>
        private void OnRecipePoolPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IRecipePoolService.CurrentPoolName))
                _ = TryRestoreSnapshotAsync();
        }

        /// <summary>
        /// 从持久化 CAD 对齐配置文件恢复变换快照。
        /// 若 CadAlignment 页面已发布更新快照，则以内存中较新的有效快照为准（不覆盖）。
        /// </summary>
        private async Task TryRestoreSnapshotAsync()
        {
            try
            {
                var restored = await CadAlignTransformSnapshotLoader
                    .TryLoadFromPersistedConfigAsync(_recipePoolService)
                    .ConfigureAwait(false);

                if (!restored.IsValid)
                {
                    _logger?.Info("[CadAlignTransform] 持久化配置无有效变换快照，保持当前状态");
                    return;
                }

                // 当前快照已有效且与恢复结果一致时跳过，避免重复发布事件
                if (CurrentSnapshot.IsValid &&
                    CurrentSnapshot.Mox == restored.Mox &&
                    CurrentSnapshot.Moy == restored.Moy &&
                    CurrentSnapshot.DeltaX == restored.DeltaX &&
                    CurrentSnapshot.DeltaY == restored.DeltaY)
                {
                    return;
                }

                UpdateSnapshot(restored);
                _logger?.Info($"[CadAlignTransform] 已从持久化配置恢复变换快照，回转中心=({restored.Mox:F3}, {restored.Moy:F3})");
            }
            catch (System.Exception ex)
            {
                _logger?.Warn($"[CadAlignTransform] 恢复变换快照失败: {ex.Message}");
            }
        }
    }
}
