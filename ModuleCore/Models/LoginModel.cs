using Framework.Mvvm;
using Core.Utilities;
using Core.Services;
using ModuleCore.Common.Authority;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;

namespace ModuleCore.Models
{
    public class LoginModel : BindableBase
    {
        private const string AuthConfigFile = "Auth.config";
        // 自动退出时间属性
        private int _autoLogoutMinutes = 30;
        public int AutoLogoutMinutes
        {
            get => _autoLogoutMinutes;
            set
            {
                if (value < 1 || value > 1440)
                    throw new ArgumentOutOfRangeException("自动注销时间应在1-1440分钟之间");
                SetProperty(ref _autoLogoutMinutes, value);
            }
        }
        public event Action OnConfigChanged;

        public LoginModel(IDialogService dialogService, ILoggerService logger)
        {
            _dialogService = dialogService;
            _logger = logger;
            //权限列表
            var authorityListString = Enum.GetNames(typeof(Authority));
            var i = 0;
            foreach (var item in authorityListString)
            {
                AuthorityList.Add(item, i);
                i++;
            }

            //已注册用户
            LoadUsers();
        }
        private readonly IDialogService _dialogService;
        private readonly ILoggerService _logger;
        #region 登录

        //登录的用户
        public event Action AuthorityChanged;
        private User _loginUser;
        public User LoginUser
        {
            get => _loginUser;
            set
            {
                if (SetProperty(ref _loginUser, value))
                {
                    AuthorityChanged?.Invoke();
                }
            }
        }
        public bool HasPermission(Authority requiredRole, bool allowHigher = true)
        {
            if (LoginUser == null) return false;

            return allowHigher ?
                LoginUser.Authority >= requiredRole :
                LoginUser.Authority == requiredRole;
        }

        #endregion 登录

        private BindingList<User> _UserList = new();

        public BindingList<User> UserList
        {
            get { return _UserList; }
            set { SetProperty(ref _UserList, value); }
        }

        private string _Name = "Admin";

        public string Name
        {
            get { return _Name; }
            set { SetProperty(ref _Name, value); }
        }

        public ObservableDictionary<string, int> AuthorityList { get; set; } = new();

        private int _AuthoritySelect;

        public int AuthoritySelect
        {
            get { return _AuthoritySelect; }
            set { SetProperty(ref _AuthoritySelect, value); }
        }


        private DataTable dt;

        public void LoadUsers()
        {
            LoadAuthConfig();

            // 从启动目录加载权限文件
            var authorityFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Authority.dll");
            dt = JsonService.DataTableFromEncryptFile(authorityFilePath);

            if (dt is null)
            {
                //First run

                dt = new DataTable();

                dt.Columns.Add("Name", Type.GetType("System.String"));
                dt.Columns.Add("Password", Type.GetType("System.String"));
                dt.Columns.Add("Authority", Type.GetType("System.Int64"));

                var password = EncryptService.Encrypt("Admin");

                AddUser(new("Guest", "password", Authority.Operator));
                AddUser(new(Name, password, Authority.Administrator));
            }
            else
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    var name = dt.Rows[i]["Name"].ToString();
                    var password = dt.Rows[i]["Password"].ToString();
                    var authority = (Authority)Convert.ToInt32((long)dt.Rows[i]["Authority"]);
                    var loaduser = new User(name, password, authority);
                    loaduser.DeleteMe += DeleteUser;
                    loaduser.ChangeMyPassword += ChangePassword;

                    UserList.Add(loaduser);
                }
            }

            LoginUser = UserList.Where(u => u.Name == "Guest").FirstOrDefault();
#if DEBUG
            //LoginUser = UserList.Where(u => u.Name == "Admin").FirstOrDefault();
#else
            LoginUser = UserList.Where(u => u.Name == "Guest").FirstOrDefault();
#endif
        }

        public string LoadPassword()
        {
            var user = UserList.Where(u => u.Name == Name).FirstOrDefault();
            return user.Password;
        }

        public void AddUser(User user)
        {
            user.DeleteMe += DeleteUser;
            UserList.Add(user);
            SaveUsers();
            UserList.Clear();
            LoadUsers();
        }

        private void ChangePassword(string userName)
        {
            if (userName == "Admin" || userName == "Guest") return;
            _dialogService.ShowDialog("PasswordChange", new DialogParameters($"name={userName}"), r =>
            {



            });
        }

        private void DeleteUser(string userName)
        {
            if (userName == "Admin" || userName == "Guest") return;

            var user = UserList.Where(u => u.Name == userName);
            if (user.Any())
            {
                UserList.Remove(user.FirstOrDefault());
                SaveUsers();
            }
        }

        public void SaveUsers()
        {
            SaveUserData();
            SaveAuthConfig();
        }
        private void SaveUserData()
        {
            dt.Rows.Clear();
            for (int i = 0; i < UserList.Count; i++)
            {
                DataRow dr = dt.NewRow();
                dt.Rows.Add(dr);
                dt.Rows[i]["Name"] = UserList[i].Name;
                dt.Rows[i]["Password"] = UserList[i].Password;
                dt.Rows[i]["Authority"] = (int)UserList[i].Authority;
            }
            var authorityFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Authority.dll");
            JsonService.DataTableToEncryptFile(authorityFilePath, dt);
        }
        private void LoadAuthConfig()
        {
            try
            {
                // 组合完整配置文件路径：启动目录/Auth.config
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AuthConfigFile);

                // 如果文件不存在，使用默认配置
                if (!File.Exists(configPath))
                {
                    InitializeDefaultConfig();
                    return;
                }

                // 加载配置表
                var configTable = JsonService.DataTableFromEncryptFile(configPath);
                if (configTable?.Rows.Count > 0 && configTable.Columns.Contains("AutoLogoutMinutes"))
                {
                    // 使用安全类型转换
                    object value = configTable.Rows[0]["AutoLogoutMinutes"];
                    if (value != null && value != DBNull.Value)
                    {
                        _autoLogoutMinutes = Convert.ToInt32(value); // ✅ 强制转换为 int
                    }
                    else
                    {
                        HandleCorruptedConfig(configPath);
                    }
                }
                else
                {
                    InitializeDefaultConfig();
                }

            }
            catch (FileNotFoundException)
            {
                InitializeDefaultConfig();
            }
        }
        private void HandleCorruptedConfig(string configPath)
        {
            try
            {
                // 创建备份文件路径
                string backupPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    $"Corrupted_Auth_{DateTime.Now:yyyyMMddHHmmss}.config");

                // 移动损坏的文件
                File.Move(configPath, backupPath);
                _logger.Warn($"检测到损坏的配置文件，已备份到: {backupPath}");
            }
            catch (Exception ex)
            {
                _logger.Error($"备份损坏配置文件失败: {ex}");
            }
            finally
            {
                InitializeDefaultConfig();
            }
        }
        private void InitializeDefaultConfig()
        {
            _autoLogoutMinutes = 30;
            SaveAuthConfig(); // 创建初始配置文件
        }
        public void SaveAuthConfig()
        {
            try
            {
                var authTable = new DataTable();
                authTable.Columns.Add("AutoLogoutMinutes", typeof(int));
                authTable.Columns.Add("LastAuthModified", typeof(DateTime));

                var row = authTable.NewRow();
                row["AutoLogoutMinutes"] = AutoLogoutMinutes;
                row["LastAuthModified"] = DateTime.UtcNow;
                authTable.Rows.Add(row);

                // 保存到启动目录/Auth.config
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AuthConfigFile);
                JsonService.DataTableToEncryptFile(configPath, authTable);

                _logger.Info($"认证配置已保存: 自动注销时间 = {_autoLogoutMinutes}分钟");
                OnConfigChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.Error($"保存认证配置失败: {ex}");
            }
        }

    }
}