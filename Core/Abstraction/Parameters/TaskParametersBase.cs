// Core.Abstraction/Parameters/TaskParametersBase.cs
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Core.Abstraction
{
    /// <summary>
    /// 任务参数基类 - 所有参数类的基类
    /// </summary>
    public abstract class TaskParametersBase : IParameterStore, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _configVersion = "1.0";
        private DateTime _lastModified = DateTime.UtcNow;

        /// <summary>
        /// 参数唯一标识（由派生类实现）
        /// </summary>
        [JsonIgnore]
        public abstract string Identifier { get; }

        /// <summary>
        /// 参数版本
        /// </summary>
        [Description("参数版本")]
        [JsonProperty("configVersion")]
        public string ConfigVersion
        {
            get => _configVersion;
            set => SetProperty(ref _configVersion, value);
        }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        [Description("最后修改时间")]
        [JsonProperty("lastModified")]
        public DateTime LastModified
        {
            get => _lastModified;
            protected set => SetProperty(ref _lastModified, value);
        }

        /// <summary>
        /// 任务名称（可选，可由派生类覆盖）
        /// </summary>
        [Description("任务名称")]
        [JsonProperty("taskName")]
        public virtual string TaskName { get; set; } = "Unnamed Task";

        /// <summary>
        /// 任务ID（可选，可由派生类覆盖）
        /// </summary>
        [Description("任务ID")]
        [JsonProperty("taskId")]
        public virtual int TaskId { get; set; } = -1;

        /// <summary>
        /// 参数优先级（可选，可由派生类覆盖）
        /// </summary>
        [Description("参数优先级")]
        [JsonProperty("priority")]
        public virtual int Priority { get; set; } = 1;

        #region 实现 IParameterStore

        public IParameterStore CreateSnapshot()
        {
            try
            {
                // 获取实际类型而不是基类
                Type concreteType = GetType();

                // 序列化当前对象
                string json = JsonConvert.SerializeObject(
                    this,
                    Formatting.None,
                    new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        TypeNameHandling = TypeNameHandling.Auto
                    });

                // 反序列化为相同的具体类型
                var clone = JsonConvert.DeserializeObject(
                    json,
                    concreteType,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto
                    });

                return (TaskParametersBase)clone;
            }
            catch (Exception ex)
            {
                // 添加适当的错误日志
                Debug.WriteLine($"创建快照失败: {ex.Message}");
                throw; // 或者返回null/空对象
            }
        }

        #endregion

        #region INotifyPropertyChanged 支持

        protected void SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return;

            storage = value;
            OnPropertyChanged(propertyName);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            LastModified = DateTime.UtcNow;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region 参数验证

        [JsonIgnore]
        public bool IsValid => Validate() == null;

        /// <summary>
        /// 参数验证 - 可由派生类覆盖
        /// </summary>
        public virtual string Validate()
        {
            // 基础实现 - 检查必须字段的有效性
            return string.IsNullOrWhiteSpace(TaskName) ? "任务名称不能为空" : null;
        }

        #endregion
    }
}
