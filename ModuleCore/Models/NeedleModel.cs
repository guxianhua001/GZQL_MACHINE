using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleCore.Models
{
    // NeedleHead.cs (Model 层)
    public class NeedleModel : BindableBase
    {
        private int _id;
        private int _currentCount;
        private int _maxCount = 50000; // 默认最大值
        private bool _isEnabled = true;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public int CurrentCount
        {
            get => _currentCount;
            set
            {
                SetProperty(ref _currentCount, value);
                CheckLimit();
            }
        }

        public int MaxCount
        {
            get => _maxCount;
            set
            {
                SetProperty(ref _maxCount, value);
                CheckLimit();
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private void CheckLimit()
        {
            IsEnabled = CurrentCount < MaxCount;
        }
    }

}
