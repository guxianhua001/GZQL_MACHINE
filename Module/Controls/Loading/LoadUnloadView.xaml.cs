using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace Module.Views
{
    public partial class LoadUnloadView : UserControl
    {
        private ViewModels.LoadUnloadViewModel _viewModel;

        public LoadUnloadView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SubscribeCollectionChanged();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SubscribeCollectionChanged();
        }

        private void SubscribeCollectionChanged()
        {
            // 取消旧的订阅
            if (_viewModel?.StepStatusList is INotifyCollectionChanged oldCollection)
                oldCollection.CollectionChanged -= OnCollectionChanged;

            _viewModel = DataContext as ViewModels.LoadUnloadViewModel;
            if (_viewModel?.StepStatusList is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += OnCollectionChanged;
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0)
            {
                StatusListBox?.Dispatcher.Invoke(() =>
                {
                    StatusListBox.ScrollIntoView(e.NewItems[0]);
                });
            }
        }
    }
}