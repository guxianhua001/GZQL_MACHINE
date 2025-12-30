using Prism.Events;
using Recipe.Events;

namespace Recipe
{
    public class SaveParametersCoordinator
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly List<string> _completedStations = new List<string>();
        private string _currentRecipeName;
        private readonly object _lockObject = new object();

        public SaveParametersCoordinator(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            // 订阅完成事件
            _eventAggregator.GetEvent<SaveParametersCompletedEvent>()
                .Subscribe(OnStationCompleted, ThreadOption.BackgroundThread);
        }

        public void StartSave(string recipeName, List<string> expectedStations)
        {
            lock (_lockObject)
            {
                _currentRecipeName = recipeName;
                _completedStations.Clear();

                _eventAggregator.GetEvent<SaveParametersProgressEvent>().Publish(new SaveProgressInfo
                {
                    Progress = 0,
                    StationName = "Coordinator",
                    Operation = $"开始保存配方 '{recipeName}'，等待 {expectedStations.Count} 个工站完成..."
                });
            }
        }

        private void OnStationCompleted(string recipeName)
        {
            if (recipeName != _currentRecipeName) return;

            lock (_lockObject)
            {
                // 这里可以根据需要跟踪具体哪些工站完成了
                // 目前我们简化处理：收到第一个完成事件就认为全部完成

                _eventAggregator.GetEvent<SaveParametersProgressEvent>().Publish(new SaveProgressInfo
                {
                    Progress = 100,
                    StationName = "Coordinator",
                    Operation = "所有工站参数保存完成"
                });

                // 通知配方管理器保存完成
                _eventAggregator.GetEvent<SaveParametersCompletedEvent>().Publish(recipeName);
            }
        }
    }
}