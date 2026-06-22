using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ModuleCore.Models
{
    public class ErrModel : BindableBase
    {
        public ErrModel()
        {
            AutoConfirm();
        }

        /// <summary>自动倒计时确认：从10秒开始倒数，到期后自动执行确认</summary>
        private async void AutoConfirm()
        {
            for (int i = 10; i >= 0; i--)
            {
                // 使用多语言资源键，避免硬编码中文
                var prefix = Application.Current?.TryFindResource("ErrModel_ConfirmPrefix") as string ?? "Confirm";
                ConfirmTime = $"{prefix} {i}";
                await Task.Delay(1000);
            }

            ExecuteConfirm();
        }

        private string _ConfirmTime;

        public string ConfirmTime
        {
            get { return _ConfirmTime; }
            set { SetProperty(ref _ConfirmTime, value); }
        }

        private string _ErrMsg;

        public string ErrMsg
        {
            get { return _ErrMsg; }
            set { SetProperty(ref _ErrMsg, value); }
        }

        private DelegateCommand _Confirm;

        public DelegateCommand Confirm =>
             _Confirm ??= new DelegateCommand(ExecuteConfirm);

        private void ExecuteConfirm()
        {
            ErrMsg = "";
            Confirmed?.Invoke();
        }

        public event Action Confirmed;
    }
}