using Prism.Mvvm;

namespace Core.Models
{
    // 步骤状态项
    public class StepStatusItem : BindableBase
    {
        private string _description;
        private bool _isCompleted;
        private bool _isCurrent;

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set => SetProperty(ref _isCompleted, value);
        }

        public bool IsCurrent
        {
            get => _isCurrent;
            set => SetProperty(ref _isCurrent, value);
        }
    }
}
