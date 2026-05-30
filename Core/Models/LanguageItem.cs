using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Core.Models
{
    /// <summary>
    /// 语言项
    /// </summary>
    public class LanguageItem : INotifyPropertyChanged
    {
        private string _displayName;
        private string _iconPath;

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set => SetField(ref _displayName, value);
        }

        /// <summary>
        /// 区域性代码（如 zh-CN, en-US）
        /// </summary>
        public string CultureCode { get; }

        /// <summary>
        /// 图标路径
        /// </summary>
        public string IconPath
        {
            get => _iconPath;
            set => SetField(ref _iconPath, value);
        }

        /// <summary>
        /// 排序索引
        /// </summary>
        public int SortIndex { get; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// 是否默认语言
        /// </summary>
        public bool IsDefault { get; }

        public LanguageItem(string displayName, string cultureCode, string iconPath = null,
                          int sortIndex = 0, bool isDefault = false, bool isEnabled = true)
        {
            DisplayName = displayName;
            CultureCode = cultureCode;
            IconPath = iconPath ?? "/Assets/Flags/default.png"; // 默认图标
            SortIndex = sortIndex;
            IsDefault = isDefault;
            IsEnabled = isEnabled;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is LanguageItem other && CultureCode == other.CultureCode;
        }

        public override int GetHashCode()
        {
            return CultureCode.GetHashCode();
        }
    }
}