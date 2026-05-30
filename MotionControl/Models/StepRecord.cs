using Prism.Mvvm;
namespace MotionControl.Models
{
    public class StepRecord : BindableBase
    {
        public string StepName { get; set; }
        private int _retryCount;
        public int RetryCount
        {
            get => _retryCount;
            set => SetProperty(ref _retryCount, value);
        }
        private bool _isCurrent;
        public bool IsCurrent
        {
            get => _isCurrent;
            set => SetProperty(ref _isCurrent, value);
        }
        private string _durationText;
        public string DurationText
        {
            get => _durationText;
            set => SetProperty(ref _durationText, value);
        }
        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }
    }
}
