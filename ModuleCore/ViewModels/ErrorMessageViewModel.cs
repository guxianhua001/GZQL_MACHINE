using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ModuleCore.ViewModels
{
    public class ErrorMessageViewModel : BindableBase, IDialogAware
    {
        public string Title { get; set; }
        public string Message { get; set; }

        public event Action<IDialogResult> RequestClose;

        public string[] BTMessage { get; set; }

        public Visibility[] BTVisibility { get; set; }

        public Visibility CloseVisible { get; set; } = Visibility.Collapsed;
        public System.Windows.Media.SolidColorBrush TitleColor { get; set; }

        private DelegateCommand[] _BTCommand;
        public DelegateCommand[] BTCommands => _BTCommand ??= new DelegateCommand[4];
        private DelegateCommand _CloseCommand { get; set; }
        public DelegateCommand CloseCommand =>
                 _CloseCommand ??= new DelegateCommand(CloseCommandImpl);

        public int Result { get; set; } = -1;

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            Title = parameters.GetValue<string>("title");
            Message = parameters.GetValue<string>("message");
        }
        /// <summary>
        /// 提示信息框构造函数，用于显示错误信息框。
        /// </summary>
        /// <param name="title"></param>
        /// <param name="msg"></param>
        /// <param name="bt1"></param>
        /// <param name="bt2"></param>
        /// <param name="bt3"></param>
        /// <param name="bt4"></param>
        /// <param name="level">Tile背景颜色，0/1/2代表绿/黄/红</param>
        public ErrorMessageViewModel(string title, string msg, int level, string bt1 = "", string bt2 = "", string bt3 = "", string bt4 = "")
        {
            Title = title;
            Message = msg;
            BTMessage = new string[4] { bt1, bt2, bt3, bt4 };
            BTVisibility = new Visibility[4] { Visibility.Collapsed, Visibility.Collapsed, Visibility.Collapsed, Visibility.Collapsed };
            for (int i = 0; i < 4; i++)
            {
                if (BTMessage[i] != "")
                    BTVisibility[i] = Visibility.Visible;
            }
            // 初始化 DelegateCommand 数组
            for (int i = 0; i < 4; i++)
            {
                int commandParameter = i + 1; // 根据索引设置参数
                BTCommands[i] = new DelegateCommand(() => CommandImpl(commandParameter));
            }
            if (bt1 == "" && bt2 == "" && bt3 == "" && bt4 == "")
                CloseVisible = Visibility.Visible;

            if (level == 0)
                TitleColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            else if (level == 1)
                TitleColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow);
            else
                TitleColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
        }
        void CommandImpl(int bt)
        {
            Result = bt;
        }

        void CloseCommandImpl()
        {
            Result = 0;
        }
    }
}
