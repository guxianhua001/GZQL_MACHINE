using Prism.Commands;
using Prism.Mvvm;
using System;

namespace ModuleCore.Common.Authority
{
    public class User : BindableBase
    {
        public User(string name, string password, Authority authority)
        {
            Name = name;
            Password = password;
            Authority = authority;
        }
        private string _name;
        private Authority _authority;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        public string Password { get; set; }
        public string PasswordHash { get; }
        public byte[] Salt { get; }

        public Authority Authority
        {
            get => _authority;
            set => SetProperty(ref _authority, value);
        }

        private DelegateCommand _Delete;
        public DelegateCommand Delete =>
             _Delete ??= new DelegateCommand(ExecuteDelete);

        void ExecuteDelete()
        {
            DeleteMe?.Invoke(Name);
        }

        public event Action<string> DeleteMe;

        private DelegateCommand _ChangePassword;
        public DelegateCommand ChangePassword =>
             _ChangePassword ??= new DelegateCommand(ExecuteChangePassword);

        void ExecuteChangePassword()
        {
            ChangeMyPassword?.Invoke(Name);
        }

        public event Action<string> ChangeMyPassword;

        public override string ToString()
        {
            return $"{Name}";
        }

    }
}
