using System.Windows;
using Core.Abstraction;

namespace MotionControl.Dialogs
{
    public partial class ParameterProgressDialog : Window, IProgressReporter
    {
        public ParameterProgressDialog(string title)
        {
            InitializeComponent();
            Title = title;
        }

        public void SetStatus(string status) => StatusText.Text = status;

        public void SetProgress(double value)
        {
            ProgressBar.Value = value;
            ProgressText.Text = $"{value * 100:0}%";
        }

        public void Report(double progress, string statusMessage = null)
        {
            Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(statusMessage))
                    SetStatus(statusMessage);
                SetProgress(progress);
            });
        }
    }
}