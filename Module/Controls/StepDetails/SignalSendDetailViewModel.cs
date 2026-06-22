using Core.Abstraction;
using Prism.Commands;
using Prism.Mvvm;
using StationTasks.Models;
using System;
using System.Windows.Input;

namespace Module.ViewModels
{
    /// <summary>
    /// 发送信号步骤（SIGNAL_SEND）编辑器 ViewModel。
    /// 配置 SignalDetail.SignalName 和 Description，保存后写入 Step.SignalDetail。
    /// </summary>
    public class SignalSendDetailViewModel : BindableBase, IDialogCloseable
    {
        private ProcessStep _step;

        /// <summary>请求关闭对话框时触发</summary>
        public event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框</summary>
        public bool CanCloseDialog() => true;

        /// <summary> 当前编辑的工艺步骤，设置时自动初始化信号配置 </summary>
        public ProcessStep Step
        {
            get => _step;
            set
            {
                if (SetProperty(ref _step, value))
                {
                    InitializeFromStep();
                    RaisePropertyChanged(nameof(StepDescription));
                }
            }
        }

        /// <summary> 步骤描述文本（用于标题栏显示） </summary>
        public string StepDescription => _step == null ? "—" : $"Seq{_step.Seq} → 发送信号";

        private string _signalName;
        /// <summary> 信号名称（发送方和等待方必须一致） </summary>
        public string SignalName
        {
            get => _signalName;
            set => SetProperty(ref _signalName, value);
        }

        private string _description;
        /// <summary> 信号说明（可选备注） </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }

        public SignalSendDetailViewModel()
        {
            SaveCommand = new DelegateCommand(OnSave);
            CloseCommand = new DelegateCommand(OnClose);
        }

        /// <summary>
        /// 从 Step.SignalDetail 加载配置，为空则创建默认实例
        /// </summary>
        private void InitializeFromStep()
        {
            if (_step == null) return;

            if (_step.SignalDetail == null)
            {
                _step.SignalDetail = new SignalDetail();
            }

            SignalName = _step.SignalDetail.SignalName;
            Description = _step.SignalDetail.Description;
        }

        /// <summary>
        /// 保存当前配置到 Step.SignalDetail 并关闭弹窗
        /// </summary>
        private void OnSave()
        {
            if (_step == null) return;

            if (_step.SignalDetail == null)
                _step.SignalDetail = new SignalDetail();

            _step.SignalDetail.SignalName = SignalName;
            _step.SignalDetail.Description = Description;

            RequestClose?.Invoke(true);
        }

        /// <summary> 关闭弹窗不保存 </summary>
        private void OnClose()
        {
            RequestClose?.Invoke(false);
        }
    }
}
