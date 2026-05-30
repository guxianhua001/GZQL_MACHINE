using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Abstraction;
using Core.Utilities;
using Framework.Mvvm;
using Prism.Commands;
using System;
using System.Linq;
using System.Windows;

namespace AlarmModule.ViewModels
{
    public class AlarmListViewModel : ViewModelBase
    {
        private readonly IAlarmService _alarmService;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localizationService;

        private AlarmRecord? _selectedAlarm;
        private int _unconfirmedCount;

        public AlarmRecord? SelectedAlarm
        {
            get => _selectedAlarm;
            set => SetProperty(ref _selectedAlarm, value);
        }

        public int UnconfirmedCount
        {
            get => _unconfirmedCount;
            set => SetProperty(ref _unconfirmedCount, value);
        }

        public string UnconfirmedCountText
        {
            get
            {
                var template = _localizationService.GetResourceOrDefault("AlarmUnconfirmedCount", "{0} 条未确认");
                return string.Format(template, UnconfirmedCount);
            }
        }

        public System.Collections.ObjectModel.ObservableCollection<AlarmRecord> ActiveAlarms => _alarmService.ActiveAlarms;

        public DelegateCommand<AlarmRecord> ConfirmCommand { get; }
        public DelegateCommand<AlarmRecord> ResetCommand { get; }
        public DelegateCommand<AlarmRecord> EliminateCommand { get; }
        public DelegateCommand ConfirmAllCommand { get; }
        public DelegateCommand ResetAllCommand { get; }
        public DelegateCommand RefreshCommand { get; }

        public AlarmListViewModel(IAlarmService alarmService, ILoggerService logger, ILocalizationService localizationService)
        {
            _alarmService = alarmService;
            _logger = logger;
            _localizationService = localizationService;

            ConfirmCommand = new DelegateCommand<AlarmRecord>(OnConfirm, CanConfirm);
            ResetCommand = new DelegateCommand<AlarmRecord>(OnReset, CanReset);
            EliminateCommand = new DelegateCommand<AlarmRecord>(OnEliminate, CanEliminate);
            ConfirmAllCommand = new DelegateCommand(OnConfirmAll, () => UnconfirmedCount > 0);
            ResetAllCommand = new DelegateCommand(OnResetAll, () => ActiveAlarms.Any(a => a.Status == AlarmStatus.Confirmed));
            RefreshCommand = new DelegateCommand(OnRefresh);

            _alarmService.AlarmTriggered += OnAlarmTriggered;

            RefreshCommand.Execute();
        }

        private void OnAlarmTriggered(AlarmRecord alarm)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateUnconfirmedCount();
                ConfirmAllCommand.RaiseCanExecuteChanged();
                ResetAllCommand.RaiseCanExecuteChanged();
            });
        }

        private async void OnConfirm(AlarmRecord alarm)
        {
            try
            {
                await _alarmService.ConfirmAsync(alarm.Id, Environment.UserName);
                UpdateUnconfirmedCount();
                RaiseCanExecuteChanged();
                _logger.Info($"已确认报警：{alarm.AlarmCode}@{alarm.AlarmSource}");
            }
            catch (Exception ex)
            {
                _logger.Error($"确认报警失败：{ex.Message}");
            }
        }

        private bool CanConfirm(AlarmRecord? alarm)
        {
            return alarm != null && alarm.Status == AlarmStatus.Unconfirmed;
        }

        private async void OnReset(AlarmRecord alarm)
        {
            try
            {
                await _alarmService.ResetAsync(alarm.Id, Environment.UserName);
                UpdateUnconfirmedCount();
                RaiseCanExecuteChanged();
                _logger.Info($"已复位报警：{alarm.AlarmCode}@{alarm.AlarmSource}");
            }
            catch (Exception ex)
            {
                _logger.Error($"复位报警失败：{ex.Message}");
            }
        }

        private bool CanReset(AlarmRecord? alarm)
        {
            return alarm != null && alarm.Status == AlarmStatus.Confirmed;
        }

        private async void OnEliminate(AlarmRecord alarm)
        {
            try
            {
                await _alarmService.EliminateAsync(alarm.Id);
                UpdateUnconfirmedCount();
                RaiseCanExecuteChanged();
                _logger.Info($"已消除报警：{alarm.AlarmCode}@{alarm.AlarmSource}");
            }
            catch (Exception ex)
            {
                _logger.Error($"消除报警失败：{ex.Message}");
            }
        }

        private bool CanEliminate(AlarmRecord? alarm)
        {
            return alarm != null && alarm.Status != AlarmStatus.Eliminated;
        }

        private async void OnConfirmAll()
        {
            try
            {
                await _alarmService.ConfirmAllAsync(Environment.UserName);
                UpdateUnconfirmedCount();
                RaiseCanExecuteChanged();
                _logger.Info("已确认全部未确认报警");
            }
            catch (Exception ex)
            {
                _logger.Error($"批量确认失败：{ex.Message}");
            }
        }

        private async void OnResetAll()
        {
            try
            {
                await _alarmService.ResetAllAsync(Environment.UserName);
                UpdateUnconfirmedCount();
                RaiseCanExecuteChanged();
                _logger.Info("已复位全部已确认报警");
            }
            catch (Exception ex)
            {
                _logger.Error($"批量复位失败：{ex.Message}");
            }
        }

        private async void OnRefresh()
        {
            try
            {
                await _alarmService.RefreshActiveAlarmsAsync();
                UpdateUnconfirmedCount();
                RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _logger.Error($"刷新报警列表失败：{ex.Message}");
            }
        }

        private void UpdateUnconfirmedCount()
        {
            UnconfirmedCount = _alarmService.UnconfirmedCount;
            RaisePropertyChanged(nameof(UnconfirmedCountText));
        }

        private void RaiseCanExecuteChanged()
        {
            ConfirmCommand.RaiseCanExecuteChanged();
            ResetCommand.RaiseCanExecuteChanged();
            EliminateCommand.RaiseCanExecuteChanged();
            ConfirmAllCommand.RaiseCanExecuteChanged();
            ResetAllCommand.RaiseCanExecuteChanged();
        }
    }
}
