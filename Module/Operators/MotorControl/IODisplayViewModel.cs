
using Framework.Mvvm;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using SmarterMotion;
using Stations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace Framework.ViewModels
{
    public class IODisplayViewModel : RegionViewModelBase
    {
        private readonly XDevice _device = XDevice.Instance;
        private DispatcherTimer _refreshTimer;

        public ObservableCollection<DiChannelViewItem> DIList { get; } = new();
        public ObservableCollection<DoChannelViewItem> DOList { get; } = new();

        public ICommand ToggleDoCommand { get; }

        public IODisplayViewModel(IContainerExtension container, IRegionManager regionManager) : base(regionManager)
        {
            InitializeDIList();
            InitializeDOList();
            SetupRefreshTimer();
            ToggleDoCommand = new DelegateCommand<DoChannelViewItem>(OnToggleDo);
        }

        private string _title = "IODisplay";
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }
        public event Action<IDialogResult> RequestClose;

        private void InitializeDIList()
        {
            DIList.Clear();
            foreach (var di in _device.DiMap.Values.OrderBy(d => d.SetId))
            {
                DIList.Add(new DiChannelViewItem(di));
            }
        }
        private void InitializeDOList()
        {
            DOList.Clear();
            foreach (var dO in _device.DoMap.Values.OrderBy(d => d.SetId))
            {
                DOList.Add(new DoChannelViewItem(dO));
            }
        }
        private void SetupRefreshTimer()
        {
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _refreshTimer.Tick += (s, e) => RefreshStatus();
            _refreshTimer.Start();
        }

        private void RefreshStatus()
        {
            foreach (var item in DIList) item.Refresh(_device);
            foreach (var item in DOList) item.Refresh(_device);
        }

        private void OnToggleDo(DoChannelViewItem item)
        {
            var currentDo = _device.FindDoById(item.SetId);
            currentDo.SetDo(currentDo.STS ? 0 : 1);
            item.Refresh(_device);
        }
        // 在ViewModel中
        //public ICommand ShowDetailsCommand => new DelegateCommand<DiChannelViewItem>(item =>
        //{
        //    var detail = $"详细状态：\n" +
        //                 $"卡ID：{item._di.CardId}\n" +
        //                 $"最后更新：{DateTime.Now:HH:mm:ss}";
        //    System.Windows.MessageBox.Show(detail);
        //});
    }

    // 数据模型（适配现有视图）
    public class DiChannelViewItem : BindableBase
    {
        private readonly XDi _di;
        public int SetId => _di.SetId;
        public int Channel => _di.Channel;
        public string Name => _di.Name;
        public string DisplayText =>
            $"{(_di.Channel > 0 ? "[扩展] " : "[常规] ")} " +
            $"ID:{_di.SetId} Port:{_di.Channel} {_di.Name} " +
            $"| 状态: {(IsActive ? "激活" : "关闭")}";

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            private set
            {
                if (SetProperty(ref _isActive, value))
                {
                    Debug.WriteLine($"DI {_di.SetId} 状态变更: {value}"); // 调试输出
                    RaisePropertyChanged(nameof(StatusColor));
                    RaisePropertyChanged(nameof(DisplayText)); // 通知文本变更
                }
            }
        }

        public DiChannelViewItem(XDi di)
        {
            _di = di;
        }

        public void Refresh(XDevice device)
        {
            int status = 0;
            device.CardMap[_di.CardId].GetDi(_di.Channel, 0, ref status);
            IsActive = status == 1;
        }

        public override string ToString() => DisplayText;

        public Brush StatusColor => IsActive
       ? Brushes.LimeGreen
       : Brushes.LightGray;


        //public override string ToString() =>
        //    $"{DisplayText} | {DateTime.Now:HH:mm:ss}";

    }

    // DO数据模型
    public class DoChannelViewItem : BindableBase
    {
        private readonly XDo _do;

        public int SetId => _do.SetId;
        public int Channel => _do.Channel;
        public string Name => _do.Name;
        public string ChannelType => _do.Channel > 0 ? "扩展通道_EDO" : "常规通道_DO";

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            private set => SetProperty(ref _isActive, value);
        }

        public DoChannelViewItem(XDo dO)
        {
            _do = dO;
        }

        //public void Refresh(XDevice device)
        //{
        //    int status = 0;
        //    device.CardMap[_do.CardId].GetDo(_do.Channel, 0, ref status);
        //    IsActive = status == 1;
        //}
        public void Refresh(XDevice device)
        {
            // 只有状态变化时才触发通知
            int status = 0;
            device.CardMap[_do.CardId].GetDo(_do.Channel, 0, ref status);
            var newStatus = status == 1;

            if (IsActive != newStatus)
            {
                IsActive = newStatus;
            }
        }
    }
}
