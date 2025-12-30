using System;
using System.Windows;
using System.Windows.Controls;

namespace Framework.Views
{
    /// <summary>
    /// CheckStation3View.xaml 的交互逻辑
    /// </summary>
    public partial class AssemblyStationView : UserControl
    {
        public AssemblyStationView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            StartADValueMonitoring();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopADValueMonitoring();
        }
        /// <summary>
        /// 开始AD值监控
        /// </summary>
        private void StartADValueMonitoring()
        {
            ADValueDisplay?.StartRealTimeRefresh();
        }

        /// <summary>
        /// 停止AD值监控
        /// </summary>
        private void StopADValueMonitoring()
        {
            ADValueDisplay?.StopRealTimeRefresh();
        }
    }
}
