using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Abstraction;
using Core.Utilities;
using Framework.Mvvm;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;

namespace AlarmModule.ViewModels
{
    public class AlarmStatsViewModel : ViewModelBase
    {
        private readonly IAlarmRepository _repository;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localizationService;

        private DateTime _startDate = DateTime.Today.AddDays(-30);
        private DateTime _endDate = DateTime.Now;
        private int _maxLevelCount = 1;
        private int _maxDailyCount = 1;

        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        public ObservableCollection<LevelDistributionItem> LevelDistribution { get; } = new ObservableCollection<LevelDistributionItem>();

        public ObservableCollection<TopSourceItem> TopSources { get; } = new ObservableCollection<TopSourceItem>();

        public ObservableCollection<DailyTrendItem> DailyTrend { get; } = new ObservableCollection<DailyTrendItem>();

        public int MaxLevelCount
        {
            get => _maxLevelCount;
            set => SetProperty(ref _maxLevelCount, value);
        }

        public int MaxDailyCount
        {
            get => _maxDailyCount;
            set => SetProperty(ref _maxDailyCount, value);
        }

        public DelegateCommand RefreshCommand { get; }

        public AlarmStatsViewModel(IAlarmRepository repository, ILoggerService logger, ILocalizationService localizationService)
        {
            _repository = repository;
            _logger = logger;
            _localizationService = localizationService;

            RefreshCommand = new DelegateCommand(OnRefresh);
            RefreshCommand.Execute();
        }

        private async void OnRefresh()
        {
            try
            {
                await LoadLevelDistributionAsync();
                await LoadTopSourcesAsync();
                await LoadDailyTrendAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localizationService.GetResourceOrDefault("AlarmStats_Log_RefreshFailed", "刷新报警统计失败：{0}"), ex.Message));
            }
        }

        private string GetLevelName(AlarmLevel level)
        {
            return level switch
            {
                AlarmLevel.Emergency => _localizationService.GetResourceOrDefault("AlarmLevel_Emergency", "紧急"),
                AlarmLevel.Serious => _localizationService.GetResourceOrDefault("AlarmLevel_Serious", "严重"),
                AlarmLevel.General => _localizationService.GetResourceOrDefault("AlarmLevel_General", "一般"),
                AlarmLevel.Prompt => _localizationService.GetResourceOrDefault("AlarmLevel_Prompt", "提示"),
                _ => level.ToString()
            };
        }

        private async System.Threading.Tasks.Task LoadLevelDistributionAsync()
        {
            var data = await _repository.GetLevelDistributionAsync(StartDate, EndDate);

            LevelDistribution.Clear();

            var levelBrushes = new Dictionary<AlarmLevel, Brush>
            {
                { AlarmLevel.Emergency, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1744")) },
                { AlarmLevel.Serious, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9100")) },
                { AlarmLevel.General, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD600")) },
                { AlarmLevel.Prompt, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2979FF")) }
            };

            foreach (AlarmLevel level in Enum.GetValues(typeof(AlarmLevel)))
            {
                var count = data.ContainsKey(level) ? data[level] : 0;
                LevelDistribution.Add(new LevelDistributionItem
                {
                    Key = GetLevelName(level),
                    Value = count,
                    KeyBrush = levelBrushes.GetValueOrDefault(level, Brushes.Gray)
                });
            }

            MaxLevelCount = Math.Max(LevelDistribution.Max(x => x.Value), 1);
        }

        private async System.Threading.Tasks.Task LoadTopSourcesAsync()
        {
            var data = await _repository.GetTopSourcesAsync(10, StartDate, EndDate);

            TopSources.Clear();
            foreach (var item in data)
            {
                TopSources.Add(new TopSourceItem
                {
                    Source = item.Source,
                    Count = item.Count
                });
            }
        }

        private async System.Threading.Tasks.Task LoadDailyTrendAsync()
        {
            var data = await _repository.GetDailyTrendAsync(7);

            DailyTrend.Clear();
            foreach (var item in data)
            {
                DailyTrend.Add(new DailyTrendItem
                {
                    Date = item.Date,
                    Count = item.Count
                });
            }

            MaxDailyCount = Math.Max(DailyTrend.Max(x => x.Count), 1);
        }
    }

    public class LevelDistributionItem
    {
        public string Key { get; set; } = string.Empty;
        public int Value { get; set; }
        public Brush KeyBrush { get; set; } = Brushes.Gray;
    }

    public class TopSourceItem
    {
        public string Source { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class DailyTrendItem
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }
}
