// ParameterEditorViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Core.Abstraction;
using Core.Models;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using PropertyChanged;

namespace Framework.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class ParameterEditorViewModel : BindableBase, IDialogAware
    {
        private readonly IParameterService _parameterService;
        private bool _isLoading;

        public string Title { get; set; } = "参数设置";
        public ObservableCollection<ParameterGroup> ParameterGroups { get; } = new ObservableCollection<ParameterGroup>();

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _searchText;

        public event Action<IDialogResult> RequestClose;

        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                ApplySearchFilter();
            }
        }

        // 使用 Prism 的 DelegateCommand
        public DelegateCommand ApplyCommand { get; private set; }
        public DelegateCommand CancelCommand { get; private set; }
        public DelegateCommand ResetCommand { get; private set; }
        public DelegateCommand ClearSearchCommand { get; private set; }

        public ParameterEditorViewModel(IParameterService parameterService)
        {
            _parameterService = parameterService;

            // 初始化命令
            ApplyCommand = new DelegateCommand(ApplyChanges, CanApply)
                .ObservesProperty(() => IsModified);

            CancelCommand = new DelegateCommand(Cancel);
            ResetCommand = new DelegateCommand(ResetToDefaults);
            ClearSearchCommand = new DelegateCommand(() => SearchText = "");

            // 加载参数
            //LoadParametersAsync();
        }
        public bool IsModified => CheckIsModified();

        private bool CheckIsModified()
        {
            // 实际项目中可以比较当前值是否与默认值不同
            return true; // 简化为总是返回true
        }

        private bool CanApply() => IsModified;

        private async void LoadParametersAsync()
        {
            try
            {
                IsLoading = true;
                var groups = await _parameterService.LoadParametersAsync();

                ParameterGroups.Clear();
                foreach (var group in groups)
                {
                    ParameterGroups.Add(group);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载参数失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplySearchFilter()
        {
            if (ParameterGroups.Count == 0) return;

            var searchQuery = (SearchText ?? "").ToLower();

            foreach (var group in ParameterGroups)
            {
                foreach (var param in group.Parameters)
                {
                    // 根据搜索文本显示/隐藏参数
                    param.IsVisible = string.IsNullOrWhiteSpace(searchQuery) ||
                                     param.Name?.ToLower().Contains(searchQuery) == true ||
                                     param.DisplayName?.ToLower().Contains(searchQuery) == true ||
                                     param.Description?.ToLower().Contains(searchQuery) == true;
                }

                // 只显示至少有一个可见参数的组
                group.IsVisible = group.Parameters.Any(p => p.IsVisible);
            }
        }

        private async void ApplyChanges()
        {
            try
            {
                IsLoading = true;

                // 将UI中的值保存回编辑参数对象
                SaveToParametersObject();
                // 触发保存回调
                OnParametersSaved?.Invoke(_editingParameters);
                // 通知对话框成功关闭
                RequestClose?.Invoke(new DialogResult(ButtonResult.OK));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存参数失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        private object ConvertValueToTargetType(object value, Type targetType)
        {
            if (value == null) return null;

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            try
            {
                // 处理可空类型
                Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                // 数值类型转换
                if (underlyingType == typeof(int) && value is double doubleVal)
                {
                    return (int)Math.Round(doubleVal);
                }
                else if (underlyingType == typeof(double) && value is int intVal)
                {
                    return (double)intVal;
                }
                else if (underlyingType == typeof(float) && value is double doubleVal2)
                {
                    return (float)doubleVal2;
                }
                // 枚举类型转换
                else if (underlyingType.IsEnum)
                {
                    if (value is string stringValue)
                    {
                        return Enum.Parse(underlyingType, stringValue);
                    }
                    else
                    {
                        return Enum.ToObject(underlyingType, value);
                    }
                }
                // 使用 Convert.ChangeType 进行基本类型转换
                else
                {
                    return Convert.ChangeType(value, underlyingType);
                }
            }
            catch
            {
                return value; // 如果转换失败，返回原始值
            }
        }
        // 添加保存回调
        public Action<TaskParametersBase> OnParametersSaved;
        private void SaveToParametersObject()
        {
            if (_editingParameters == null) return;

            foreach (var group in ParameterGroups)
            {
                foreach (var param in group.Parameters)
                {
                    var property = _editingParameters.GetType().GetProperty(param.Name);
                    if (property != null && property.CanWrite)
                    {
                        // 特殊处理 List<PointF> 类型
                        if (property.PropertyType == typeof(List<PointF>))
                        {
                            // 对于 DispensingPath，我们不需要从 UI 设置值
                            // 因为它有自己的序列化属性 DispensingPathSerialized
                            continue;
                        }

                        var convertedValue = ConvertValueToTargetType(param.Value, property.PropertyType);
                        property.SetValue(_editingParameters, convertedValue);
                    }
                }
            }
        }
        private async void ResetToDefaults()
        {
            try
            {
                IsLoading = true;
                var groups = await _parameterService.ResetToDefaultsAsync();

                ParameterGroups.Clear();
                foreach (var group in groups)
                {
                    ParameterGroups.Add(group);
                }

                MessageBox.Show("所有参数已重置为默认值", "重置完成",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重置参数失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        private void LoadParametersFromObject(TaskParametersBase parameters)
        {
            ParameterGroups.Clear();
            if (parameters == null) return;

            // 分组字典：<Category, 参数列表>
            var categoryDict = new Dictionary<string, List<ParameterItem>>();

            var properties = parameters.GetType().GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (var property in properties)
            {
                if (!property.CanRead) continue;

                // 获取Category或使用默认
                var categoryAttr = property.GetCustomAttribute<CategoryAttribute>();
                string category = categoryAttr?.Category ?? "默认设置";

                if (!categoryDict.ContainsKey(category))
                {
                    categoryDict[category] = new List<ParameterItem>();
                }

                ParameterItem item = CreateParameterItem(property, parameters);
                if (item != null)
                {
                    categoryDict[category].Add(item);
                }
            }

            // 创建参数组
            foreach (var kvp in categoryDict)
            {
                ParameterGroups.Add(new ParameterGroup(kvp.Key, kvp.Value));
            }
        }


        private ParameterItem CreateParameterItem(PropertyInfo property, object source)
        {
            try
            {
                var value = property.GetValue(source);
                Console.WriteLine($"处理属性: {property.Name}, 类型: {property.PropertyType}, 值: {value}");

                // 特殊处理：跳过 List<PointF> 类型的属性
                if (property.PropertyType == typeof(List<PointF>))
                {
                    Console.WriteLine($"跳过 List<PointF> 属性: {property.Name}");
                    return null; // 返回 null 表示跳过此属性
                }

                // 添加对数值范围的支持
                var rangeAttr = property.GetCustomAttribute<RangeAttribute>();

                // 获取显示格式属性
                var displayFormatAttr = property.GetCustomAttribute<DisplayFormatAttribute>();
                string formatString = displayFormatAttr?.DataFormatString ?? "F";

                // 检查是否为只读属性
                bool isReadOnly = property.Name == "TaskId" ||
                                !property.CanWrite ||
                                property.GetSetMethod() == null;
                // 处理枚举类型
                if (property.PropertyType.IsEnum)
                {
                    Console.WriteLine($"创建枚举参数: {property.Name}");

                    var values = Enum.GetValues(property.PropertyType).Cast<object>().ToList();
                    var item = new EnumParameterItem
                    {
                        Name = property.Name,
                        DisplayName = GetDisplayName(property),
                        Description = GetDescription(property),
                        Value = value,
                        EnumValues = values,
                        EnumType = property.PropertyType,
                        OriginalType = property.PropertyType  // 保存原始类型
                    };
                    return item;
                }

                // 处理数字类型（int/double/float）
                if (property.PropertyType == typeof(int) ||
                        property.PropertyType == typeof(double) ||
                        property.PropertyType == typeof(float))
                {
                    var numberParam = new NumberParameterItem
                    {
                        Name = property.Name,
                        DisplayName = GetDisplayName(property),
                        Description = GetDescription(property),
                        MinValue = rangeAttr?.Minimum as double? ?? (property.PropertyType == typeof(int) ? 0 : 0.0),
                        MaxValue = rangeAttr?.Maximum as double? ?? (property.PropertyType == typeof(int) ? 10000 : 10000.0),
                        IsEditable = !isReadOnly,
                        FormatString = formatString, // 设置格式字符串
                        OriginalType = property.PropertyType  // 保存原始类型
                    };
                    // 设置小数位
                    if (property.PropertyType == typeof(int))
                    {
                        numberParam.DecimalPlaces = 0; // 整型不显示小数位
                    }
                    else
                    {
                        // 根据格式字符串确定小数位数
                        if (formatString.StartsWith("F0")) numberParam.DecimalPlaces = 0;
                        else if (formatString.StartsWith("F1")) numberParam.DecimalPlaces = 1;
                        else if (formatString.StartsWith("F2")) numberParam.DecimalPlaces = 2;
                        else if (formatString.StartsWith("F3")) numberParam.DecimalPlaces = 3;
                        else numberParam.DecimalPlaces = 2; // 默认
                    }
                    // 设置值（必须放在小数位设置后）
                    numberParam.ActualValue = Convert.ToDouble(value);
                    return numberParam;
                }

                // 处理布尔值
                if (property.PropertyType == typeof(bool))
                {
                    Console.WriteLine($"创建布尔参数: {property.Name}");
                    return new BooleanParameterItem
                    {
                        Name = property.Name,
                        DisplayName = GetDisplayName(property),
                        Description = GetDescription(property),
                        Value = (bool)value,
                        OriginalType = property.PropertyType  // 保存原始类型
                    };
                }

                // 创建后备类型
                Console.WriteLine($"创建后备字符串参数: {property.Name}");
                return new StringParameterItem
                {
                    Name = property.Name,
                    DisplayName = GetDisplayName(property),
                    Description = GetDescription(property),
                    Value = value?.ToString() ?? "null",
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"参数创建错误: {ex.Message}");
                return new StringParameterItem
                {
                    Name = property.Name,
                    DisplayName = $"错误: {property.Name}",
                    Value = $"创建失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 从属性获取显示名称
        /// </summary>
        private string GetDisplayName(PropertyInfo property)
        {
            var displayAttr = property.GetCustomAttribute<DisplayNameAttribute>();
            return displayAttr?.DisplayName ?? property.Name;
        }

        /// <summary>
        /// 从属性获取描述信息
        /// </summary>
        private string GetDescription(PropertyInfo property)
        {
            var descriptionAttr = property.GetCustomAttribute<DescriptionAttribute>();
            return descriptionAttr?.Description ?? string.Empty;
        }

        private void Cancel()
        {
            CloseWindow();
        }

        private void CloseWindow()
        {
            var window = Application.Current.Windows
                                .OfType<Window>()
                                .FirstOrDefault(w => w.IsActive && w.DataContext == this);
            window?.Close();
        }

        // 添加加载指示器页面
        private void NavigateToBusyView()
        {
            var busyView = new Views.BusyIndicatorView();
            // 这里可以使用Prism导航或直接显示窗口
            // 简化为不实现
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // 统一使用小写键名获取参数
            if (navigationContext.Parameters.TryGetValue("title", out string title))
            {
                Title = title;
            }

            // 添加参数对象接收
            if (navigationContext.Parameters.TryGetValue("parameters", out TaskParametersBase parameters))
            {
                // 从传入参数加载数据（替换原有的从服务加载）
                //LoadParametersFromObject(parameters); 
            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext) { }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }

        private TaskParametersBase _editingParameters;
        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.TryGetValue("title", out string title))
                Title = title;
            if (parameters.TryGetValue("parameters", out TaskParametersBase parametersObj))
            {
                _editingParameters = parametersObj;
                LoadParametersFromObject(_editingParameters);
            }
            else
            {
                //LoadParametersFromService();
            }
            // 获取保存回调
            if (parameters.TryGetValue("onSaved", out Action<TaskParametersBase> onSaved))
            {
                OnParametersSaved = onSaved;
            }
        }
    }
}
