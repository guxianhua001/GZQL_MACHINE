// Core.Models/TreeNode.cs
using Prism.Mvvm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Core.Models
{
    public class TreeNode : BindableBase
    {
        private string _name;
        private string _path;
        private string _icon;
        private string _viewType;
        private bool _isExpanded;
        private bool _isSelected;
        private string _localizationKey;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        // 本地化键，用于从资源文件获取翻译
        public string LocalizationKey
        {
            get => _localizationKey;
            set => SetProperty(ref _localizationKey, value);
        }
        // 显示名称（本地化后的名称）
        private string _displayName;
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }
        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public string ViewType
        {
            get => _viewType;
            set => SetProperty(ref _viewType, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // 使用 IList 接口，既支持 ObservableCollection 也支持 List
        public IList<TreeNode> Children { get; set; }
        // 获取本地化显示名称
        private string GetLocalizedDisplayName()
        {
            // 如果设置了本地化键，优先使用
            if (!string.IsNullOrEmpty(LocalizationKey))
            {
                // 返回原名称
                return Name;
            }

            return Name;
        }
        // 通知 DisplayName 变化
        public void NotifyDisplayNameChanged()
        {
            RaisePropertyChanged(nameof(DisplayName));
        }

        public TreeNode()
        {
            Children = new ObservableCollection<TreeNode>();
        }

        public TreeNode(string name) : this()
        {
            Name = name;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}