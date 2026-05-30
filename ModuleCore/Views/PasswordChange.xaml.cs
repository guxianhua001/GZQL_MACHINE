using Core.Abstraction;
using Core.Services;
using ModuleCore.Models;
using ModuleCore.ViewModels;
using Prism.Ioc;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ModuleCore.Views
{
    public partial class PasswordChange : UserControl
    {
        private ILocalizationService _localizationService;

        public LoginModel Model { get; set; }

        public PasswordChange(IContainerExtension container)
        {
            Model = container.Resolve<LoginModel>();
            _localizationService = container.Resolve<ILocalizationService>();
            InitializeComponent();
        }

        private void ChangePassword(object sender, RoutedEventArgs e)
        {
            if (isEqual)
            {
                SavePassword(passwordBoxSecond.Password);
            }
            if (!(passwordBoxFirst.Password == passwordBoxSecond.Password))
                textBlockMsg.Text = _localizationService.GetResourceOrDefault("PasswordChange_Mismatch", "两次输入不一致");
            if (!(passwordBoxFirst.Password.Length > 0))
                textBlockMsg.Text = _localizationService.GetResourceOrDefault("PasswordChange_Empty", "密码不能为空");
        }

        private bool isEqual;

        private void CheckPassword(object sender, RoutedEventArgs e)
        {
            isEqual = passwordBoxFirst.Password == passwordBoxSecond.Password && passwordBoxFirst.Password.Length > 0;

            if (!(passwordBoxFirst.Password == passwordBoxSecond.Password))
                textBlockMsg.Text = _localizationService.GetResourceOrDefault("PasswordChange_Mismatch", "两次输入不一致");
            if (!(passwordBoxFirst.Password.Length > 0))
                textBlockMsg.Text = _localizationService.GetResourceOrDefault("PasswordChange_Empty", "密码不能为空");
            if (isEqual)
                textBlockMsg.Text = "";
        }

        public void SavePassword(string password)
        {
            if (!isEqual) return;
            string passwordCryptic = EncryptService.Encrypt(password);
            var user = Model.UserList.Where(u => u.Name == UserName.Text).FirstOrDefault();
            user.Password = passwordCryptic;
            Model.SaveUsers();
            var viewModel = DataContext as PasswordChangeViewModel;
            var successMsg = _localizationService.GetResourceOrDefault("PasswordChange_Success", "修改成功");
            var successTitle = _localizationService.GetResourceOrDefault("PasswordChange_SuccessTitle", "成功");
            MessageBox.Show(successMsg, successTitle);
            viewModel.Close();
        }
    }
}
