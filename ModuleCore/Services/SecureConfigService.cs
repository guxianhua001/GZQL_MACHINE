using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ModuleCore
{
    // 新增配置服务接口
    public interface IConfigService
    {
        int GetAutoLogoutMinutes();
        void UpdateAutoLogout(int minutes);
    }

    // 使用DPAPI加密实现
    public class SecureConfigService : IConfigService
    {
        private readonly byte[] _entropy = Encoding.UTF8.GetBytes("YourSystemSalt");

        public int GetAutoLogoutMinutes()
        {
            var encrypted = File.ReadAllBytes("Config.secure");
            var decrypted = ProtectedData.Unprotect(encrypted, _entropy, DataProtectionScope.LocalMachine);
            return BitConverter.ToInt32(decrypted);
        }

        public void UpdateAutoLogout(int minutes)
        {
            var data = BitConverter.GetBytes(minutes);
            var encrypted = ProtectedData.Protect(data, _entropy, DataProtectionScope.LocalMachine);
            File.WriteAllBytes("Config.secure", encrypted);
        }
    }

}
