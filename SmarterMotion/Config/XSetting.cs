using Core.Abstraction;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace SmarterMotion
{
    /// <summary>
    /// 配置基类
    /// </summary>
    public abstract class XSetting : INotifyPropertyChanged
    {
        /// <summary>
        /// 配置名称（用于界面显示）
        /// </summary>
        [XmlIgnore]
        [JsonIgnore]
        public string Name { get; set; }

        /// <summary>
        /// 唯一标识符 (推荐使用任务ID)
        /// </summary>
        [XmlIgnore]
        [JsonIgnore]
        public string Identifier { get; set; }

        /// <summary>
        /// 配置最后修改时间
        /// </summary>
        [XmlIgnore]
        [JsonIgnore]
        public DateTime LastModified { get; set; }

        /// <summary>
        /// 配置版本
        /// </summary>
        [XmlIgnore]
        [JsonIgnore]
        public string ConfigVersion { get; set; } = "2.0";

        /// <summary>
        /// 配置文件存储位置
        /// </summary>
        [XmlIgnore]
        [JsonIgnore]
        public DirectoryInfo StoreLocation { get; private set; }

        /// <summary>
        /// 配置变更事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<SettingChangedEventArgs> SettingChanged;

        protected XSetting()
        {
            // 设置默认存储位置
            StoreLocation = new DirectoryInfo(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config",
                this.GetType().Name));

            Directory.CreateDirectory(StoreLocation.FullName);
        }

        /// <summary>
        /// 配置迁移（版本升级时使用）
        /// </summary>
        /// <param name="oldVersion">原版本号</param>
        /// <returns>迁移后的配置对象</returns>
        protected virtual object MigrateFromVersion(string oldVersion)
        {
            // 默认实现不进行迁移
            return this;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            OnSettingChanged(propertyName);
        }

        protected virtual void OnSettingChanged(string propertyName)
        {
            SettingChanged?.Invoke(this, new SettingChangedEventArgs
            {
                PropertyName = propertyName,
                SettingName = this.Name,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// 设置存储位置
        /// </summary>
        public void SetStoreLocation(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            StoreLocation = new DirectoryInfo(directoryPath);
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public virtual async Task<bool> SaveAsync(StorageFormat format = StorageFormat.Json)
        {
            try
            {
                string filePath = GetFilePath(format);

                // 创建目录
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                switch (format)
                {
                    case StorageFormat.Json:
                        return await SaveJsonFile(filePath);
                    case StorageFormat.Xml:
                        return await SaveXmlFile(filePath);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(format), format, null);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"配置 {Name} 保存失败: {ex}");
                return false;
            }
            finally
            {
                LastModified = DateTime.Now;
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        public virtual async Task<bool> LoadAsync(StorageFormat format = StorageFormat.Json)
        {
            try
            {
                string filePath = GetFilePath(format);

                if (!File.Exists(filePath))
                {
                    return false; // 文件不存在不报错，视为等待首次保存
                }

                switch (format)
                {
                    case StorageFormat.Json:
                        return await LoadJsonFile(filePath);
                    case StorageFormat.Xml:
                        return await LoadXmlFile(filePath);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(format), format, null);
                }
            }
            catch (JsonException jsonEx) when (File.Exists(GetFilePath(StorageFormat.Json)))
            {
                // 尝试恢复备份
                return await TryRestoreFromBackup(StorageFormat.Json, jsonEx);
            }
            catch (XmlException xmlEx) when (File.Exists(GetFilePath(StorageFormat.Xml)))
            {
                // 尝试恢复备份
                return await TryRestoreFromBackup(StorageFormat.Xml, xmlEx);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"配置 {Name} 加载失败: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 创建可复制的快照副本
        /// </summary>
        /// <typeparam name="T">配置类型</typeparam>
        /// <returns>新建的副本对象</returns>
        public T Snapshot<T>() where T : XSetting
        {
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public IParameterStore CreateSnapshot()
        {
            return (IParameterStore)this.Snapshot<XSetting>();
        }

        #region 私有方法

        private string GetFilePath(StorageFormat format)
        {
            return Path.Combine(
                StoreLocation.FullName,
                $"{Identifier}_config.{format.ToString().ToLower()}");
        }

        private async Task<bool> SaveJsonFile(string filePath)
        {
            await Task.Run(() =>
            {
                // 先备份原始文件
                BackupIfExists(filePath);

                File.WriteAllText(filePath, JsonConvert.SerializeObject(
                    this,
                    new JsonSerializerSettings
                    {
                        Formatting = Newtonsoft.Json.Formatting.Indented,
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }),
                    Encoding.UTF8);
            });
            return true;
        }

        private async Task<bool> SaveXmlFile(string filePath)
        {
            await Task.Run(() =>
            {
                // 先备份原始文件
                BackupIfExists(filePath);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    var serializer = new XmlSerializer(this.GetType());
                    serializer.Serialize(stream, this);
                }
            });
            return true;
        }

        private async Task<bool> LoadJsonFile(string filePath)
        {
            await Task.Run(() =>
            {
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                JsonConvert.PopulateObject(json, this);
            });
            return true;
        }

        private async Task<bool> LoadXmlFile(string filePath)
        {
            await Task.Run(() =>
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    var serializer = new XmlSerializer(this.GetType());
                    if (serializer.Deserialize(stream) is XSetting setting)
                    {
                        foreach (var prop in setting.GetType().GetProperties())
                        {
                            var currentProp = this.GetType().GetProperty(prop.Name);
                            if (currentProp != null && currentProp.CanWrite)
                            {
                                currentProp.SetValue(this, prop.GetValue(setting));
                            }
                        }
                    }
                }
            });
            return true;
        }

        private void BackupIfExists(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                string backupDir = Path.Combine(StoreLocation.FullName, "Backups");
                Directory.CreateDirectory(backupDir);

                string backupFile = Path.Combine(
                    backupDir,
                    $"{Path.GetFileNameWithoutExtension(filePath)}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(filePath)}");

                File.Copy(filePath, backupFile);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"无法创建备份文件: {ex}");
            }
        }

        private async Task<bool> TryRestoreFromBackup(StorageFormat format, Exception originalEx)
        {
            try
            {
                Trace.TraceWarning($"配置文件损坏: {originalEx.Message}. 尝试恢复备份...");

                string backupDir = Path.Combine(StoreLocation.FullName, "Backups");
                if (!Directory.Exists(backupDir))
                {
                    Trace.TraceWarning("没有找到备份目录");
                    return false;
                }

                // 获取最新备份
                var filePattern = $"{Identifier}_config_{DateTime.Now:yyyyMMdd}_*{GetFileExtension(format)}";
                var backupFiles = Directory.GetFiles(backupDir, filePattern);

                if (backupFiles.Length == 0)
                {
                    Trace.TraceWarning("没有找到指定日期的备份");
                    return false;
                }

                // 按时间倒序排序
                //Array.Sort(backupFiles, (a, b) =>
                //    Date.GetLastWriteTime(b).CompareTo(Date.GetLastWriteTime(a)));

                // 恢复第一个备份
                string filePath = GetFilePath(format);
                File.Copy(backupFiles[0], filePath, true);

                Trace.TraceInformation($"成功恢复备份: {Path.GetFileName(backupFiles[0])}");

                // 重试加载
                return await LoadAsync(format);
            }
            catch (Exception restoreEx)
            {
                Trace.TraceError($"备份恢复失败: {restoreEx}");
                return false;
            }
        }

        private string GetFileExtension(StorageFormat format)
        {
            return format == StorageFormat.Json ? ".json" : ".xml";
        }

        #endregion
    }

    /// <summary>
    /// 配置变更事件参数
    /// </summary>
    public class SettingChangedEventArgs : EventArgs
    {
        public string SettingName { get; set; }
        public string PropertyName { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 存储格式
    /// </summary>
    public enum StorageFormat
    {
        Json,
        Xml
    }
}
