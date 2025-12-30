using Interfaces;
using Interfaces.Events;
using ModuleCore.Models;
using ModuleCore.Views;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ModuleCore.ViewModels
{
    // NeedleViewModel.cs (ViewModel 层)
    public class NeedleViewModel : BindableBase, INeedleService
    {
        private readonly string SaveFileName = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Config",
            "NeedleData.json");
        private readonly IEventAggregator _eventAggregator;
        // 添加明确的订阅初始化
        private SubscriptionToken _dialPinCountToken;

        public ObservableCollection<NeedleModel> Needles { get; }
        public DelegateCommand<NeedleModel> IncrementCommand { get; }
        public DelegateCommand<NeedleModel> ClearCommand { get; }
        public DelegateCommand<NeedleModel> ResetMaxCommand { get; }
        public NeedleViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _dialPinCountToken = _eventAggregator
               .GetEvent<DialPinCountChangedEvent>()
               .Subscribe(OnDialPinCountChanged,
                        ThreadOption.UIThread,
                        keepSubscriberReferenceAlive: true);
            Needles = new ObservableCollection<NeedleModel>
            {
                new NeedleModel { Id = 1 },
                new NeedleModel { Id = 2 },
                new NeedleModel { Id = 3 },
                new NeedleModel { Id = 4 }
            };
            IncrementCommand = new DelegateCommand<NeedleModel>(OnIncrement);
            ClearCommand = new DelegateCommand<NeedleModel>(OnClear);
            ResetMaxCommand = new DelegateCommand<NeedleModel>(OnResetMax);
            LoadData();
        }
        private void OnIncrement(NeedleModel needle)
        {
            if (needle.CurrentCount >= needle.MaxCount) return;
            needle.CurrentCount++;

            if ((double)needle.CurrentCount / needle.MaxCount >= 0.9)
            {
                _eventAggregator.GetEvent<ThresholdWarningEvent>()
                    .Publish(new ThresholdWarningNotification
                    {
                        NeedleId = needle.Id.ToString(),
                        CurrentUsage = needle.CurrentCount,
                        MaxUsage = needle.MaxCount
                    });
            }
        }
        private void OnDialPinCountChanged(DialPinCountChangedEventArgs args)
        {
            Debug.WriteLine($"收到Task{args.TaskNumber}的计数更新: {args.NewCount}");

            var needleId = args.TaskNumber - 2; // Task3->1, Task4->2...
            var needle = Needles.FirstOrDefault(n => n.Id == needleId);

            if (needle != null)
            {
                // 确保在UI线程更新（即使使用UIThread选项也再次确认）
                Application.Current.Dispatcher.Invoke(() =>
                {
                    needle.CurrentCount++;// = args.NewCount;
                    Debug.WriteLine($"更新Needle{needleId}计数: {needle.CurrentCount}");
                });
            }
        }
        public void Dispose()
        {
            _dialPinCountToken?.Dispose();
        }

        private void OnClear(NeedleModel needle) => needle.CurrentCount = 0;
        private void OnResetMax(NeedleModel needle) => needle.MaxCount = 100;
        public void SaveData()
        {
            var data = Needles.Select(n => new
            {
                n.Id,
                n.CurrentCount,
                n.MaxCount
            });
            File.WriteAllText(SaveFileName, JsonConvert.SerializeObject(data));
        }
        public void LoadData()
        {
            if (!File.Exists(SaveFileName)) return;
            var data = JsonConvert.DeserializeAnonymousType(
                File.ReadAllText(SaveFileName),
                new[] { new { Id = 0, CurrentCount = 0, MaxCount = 0 } });
            if (data == null) return;
            foreach (var item in data)
            {
                var needle = Needles.FirstOrDefault(n => n.Id == item.Id);
                if (needle != null)
                {
                    needle.CurrentCount = item.CurrentCount;
                    needle.MaxCount = item.MaxCount;
                }
            }
        }
        public void IncrementNeedleCount(int needleId)
        {
            var needle = Needles.FirstOrDefault(n => n.Id == needleId);
            if (needle != null) needle.CurrentCount++;
        }
        public void ResetNeedle(int needleId)
        {
            var needle = Needles.FirstOrDefault(n => n.Id == needleId);
            if (needle != null) needle.CurrentCount = 0;
        }

        public int GetNeedleUsageCount(int needleId)
        {
            var needle = Needles.FirstOrDefault(n => n.Id == needleId);
            return needle?.CurrentCount ?? -1; // 返回-1表示未找到
        }

        public int GetNeedleMaxCount(int needleId)
        {
            var needle = Needles.FirstOrDefault(n => n.Id == needleId);
            return needle?.MaxCount ?? -1;
        }
    }
}
