using System.Windows;
using System;
using System.Windows.Threading;

namespace Interfaces.Views
{
    public partial class LoadingDialog : Window
    {
        public event EventHandler CancelRequested;

        public string Status
        {
            get => StatusText.Text;
            set => SafeInvoke(() => StatusText.Text = value);
        }

        public int Progress
        {
            get => (int)ProgressBar.Value;
            set => SafeInvoke(() =>
            {
                ProgressBar.Value = value;
                ProgressText.Text = $"{value}%";
            });
        }

        private bool _canClose = false;

        public LoadingDialog(string title = "正在处理",
                            string status = "正在处理，请稍候...",
                            bool allowCancel = true)
        {
            InitializeComponent();

            Title = title;
            Status = status;
            CancelButton.Visibility = allowCancel ? Visibility.Visible : Visibility.Collapsed;
            CancelButton.Click += (s, e) => CancelRequested?.Invoke(this, EventArgs.Empty);

            // 防止用户直接点击关闭按钮
            Closing += (s, e) =>
            {
                if (!_canClose) e.Cancel = true;
            };
        }

        public void Report(int percentage, string message)
        {
            SafeInvoke(() =>
            {
                ProgressBar.Value = Math.Clamp(percentage, 0, 100);
                ProgressText.Text = $"{percentage}%";
                StatusText.Text = message;
            });
        }

        public void ReportProgress(int percentage, string status = null)
        {
            Dispatcher.Invoke(() =>
            {
                Progress = Math.Clamp(percentage, 0, 100);

                if (!string.IsNullOrEmpty(status))
                {
                    Status = status;
                }
            });
        }
        public void SetCompleteState(string message = "操作完成", bool autoClose = true)
        {
            SafeInvoke(() =>
            {
                StatusText.Text = message;
                ProgressBar.Value = 100;
                ProgressText.Text = "100%";
                CancelButton.Content = "关闭";
                CancelButton.Visibility = Visibility.Visible;

                // 更新按钮行为为关闭对话框
                CancelButton.Click -= CancelButton_Click;
                CancelButton.Click += (s, e) => Close();

                _canClose = true;
                if (autoClose)
                {
                    var timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(2)
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        Close();
                    };
                    timer.Start();
                }
            });
        }

        private void SafeInvoke(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                action.Invoke();
            }
            else
            {
                Dispatcher.Invoke(action);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
