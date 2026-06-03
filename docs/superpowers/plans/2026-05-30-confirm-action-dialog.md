# CustomDialog 多功能自定义弹窗 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 参考 RecoverableFaultDialogView 的视觉样式，在 ModuleCore 创建一个高度可定制的公共弹窗，支持动态按钮数量、按钮文本/颜色/图标全部可配置，替代现有 MessageDialog 深色窗口弹窗。

**Architecture:** 创建 CustomDialog（View + ViewModel），放在 ModuleCore 项目。ViewModel 使用 `ObservableCollection<DialogButton>` 管理动态按钮列表，每个按钮可独立配置文本、背景色、图标。View 使用 ItemsControl 渲染按钮行，参考 RecoverableFaultDialogView 的布局风格（图标+标题行、消息区、底部按钮行）。通过 Prism IDialogService 注册和调用。

**Tech Stack:** WPF + Prism IDialogService + MaterialDesignInXAML + PackIcon

---

## 文件结构

| 操作 | 文件路径 | 职责 |
|------|---------|------|
| 创建 | `ModuleCore/Models/DialogButton.cs` | 按钮数据模型 |
| 创建 | `ModuleCore/ViewModels/CustomDialogViewModel.cs` | 弹窗逻辑 |
| 创建 | `ModuleCore/Views/CustomDialog.xaml` | 弹窗 UI |
| 创建 | `ModuleCore/Views/CustomDialog.xaml.cs` | Code-behind |
| 修改 | `ModuleCore/ModuleCore.cs` | 注册新 Dialog |
| 修改 | `RecipeManagement/ViewModels/MultiStationPositionEditorViewModel.cs` | 替换调用 |

---

### Task 1: 创建 DialogButton 模型

**Files:**
- 创建: `ModuleCore/Models/DialogButton.cs`

- [ ] **Step 1: 编写 DialogButton**

```csharp
using MaterialDesignThemes.Wpf;
using Prism.Commands;

namespace ModuleCore.Models
{
    /// <summary>
    /// 自定义弹窗按钮数据模型
    /// 每个按钮可独立配置文本、背景色、图标、点击回调
    /// </summary>
    public class DialogButton : BindableObject
    {
        private string _text;
        /// <summary>按钮文本</summary>
        public string Text
        {
            get => _text;
            set { _text = value; RaisePropertyChanged(nameof(Text)); }
        }

        private string _background = "#757575";
        /// <summary>按钮背景色（十六进制字符串）</summary>
        public string Background
        {
            get => _background;
            set { _background = value; RaisePropertyChanged(nameof(Background)); }
        }

        private string _foreground = "White";
        /// <summary>按钮前景色</summary>
        public string Foreground
        {
            get => _foreground;
            set { _foreground = value; RaisePropertyChanged(nameof(Foreground)); }
        }

        private PackIconKind _iconKind = PackIconKind.None;
        /// <summary>按钮图标</summary>
        public PackIconKind IconKind
        {
            get => _iconKind;
            set { _iconKind = value; RaisePropertyChanged(nameof(IconKind)); }
        }

        private int _buttonIndex;
        /// <summary>按钮索引，用于返回结果</summary>
        public int ButtonIndex
        {
            get => _buttonIndex;
            set { _buttonIndex = value; RaisePropertyChanged(nameof(ButtonIndex)); }
        }

        /// <summary>点击命令，由 ViewModel 注入</summary>
        public DelegateCommand ClickCommand { get; set; }
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build ModuleCore/ModuleCore.csproj --no-restore -v q`
Expected: 编译通过

---

### Task 2: 创建 CustomDialogViewModel

**Files:**
- 创建: `ModuleCore/ViewModels/CustomDialogViewModel.cs`

- [ ] **Step 1: 编写 ViewModel**

```csharp
using System.Collections.ObjectModel;
using MaterialDesignThemes.Wpf;
using ModuleCore.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;

namespace ModuleCore.ViewModels
{
    /// <summary>
    /// 多功能自定义弹窗 ViewModel
    /// 支持可配置图标/标题/消息/动态按钮列表
    /// </summary>
    public class CustomDialogViewModel : BindableBase, IDialogAware
    {
        #region 属性

        private string _title = "提示";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private string _message;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        private PackIconKind _iconKind = PackIconKind.InfoOutline;
        /// <summary>标题区图标</summary>
        public PackIconKind IconKind
        {
            get => _iconKind;
            set => SetProperty(ref _iconKind, value);
        }

        private string _iconForeground = "#FF9800";
        /// <summary>标题区图标颜色</summary>
        public string IconForeground
        {
            get => _iconForeground;
            set => SetProperty(ref _iconForeground, value);
        }

        /// <summary>动态按钮列表</summary>
        public ObservableCollection<DialogButton> Buttons { get; } = new ObservableCollection<DialogButton>();

        #endregion

        #region IDialogAware

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            // 基础属性
            if (parameters.ContainsKey("title"))
                Title = parameters.GetValue<string>("title");
            if (parameters.ContainsKey("message"))
                Message = parameters.GetValue<string>("message");
            if (parameters.ContainsKey("iconKind"))
                IconKind = parameters.GetValue<PackIconKind>("iconKind");
            if (parameters.ContainsKey("iconForeground"))
                IconForeground = parameters.GetValue<string>("iconForeground");

            // 动态按钮列表
            if (parameters.ContainsKey("buttons"))
            {
                var buttons = parameters.GetValue<ObservableCollection<DialogButton>>("buttons");
                if (buttons != null)
                {
                    foreach (var btn in buttons)
                    {
                        // 注入点击命令
                        var capturedIndex = btn.ButtonIndex;
                        btn.ClickCommand = new DelegateCommand(() => CloseWithResult(capturedIndex));
                        Buttons.Add(btn);
                    }
                }
            }
        }

        #endregion

        public CustomDialogViewModel() { }

        /// <summary>关闭弹窗并返回按钮索引</summary>
        private void CloseWithResult(int buttonIndex)
        {
            var result = buttonIndex >= 0 ? ButtonResult.OK : ButtonResult.Cancel;
            var parameters = new DialogParameters { { "buttonIndex", buttonIndex } };
            RequestClose?.Invoke(new DialogResult(result, parameters));
        }
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build ModuleCore/ModuleCore.csproj --no-restore -v q`
Expected: 编译通过

---

### Task 3: 创建 CustomDialog View

**Files:**
- 创建: `ModuleCore/Views/CustomDialog.xaml`
- 创建: `ModuleCore/Views/CustomDialog.xaml.cs`

- [ ] **Step 1: 编写 XAML**

```xml
<UserControl x:Class="ModuleCore.Views.CustomDialog"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:prism="http://prismlibrary.com/"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
             xmlns:lang="clr-namespace:Core.Markup;assembly=Core"
             xmlns:models="clr-namespace:ModuleCore.Models"
             prism:ViewModelLocator.AutoWireViewModel="True"
             Width="480">
    <UserControl.Resources>
        <ResourceDictionary>
            <BooleanToVisibilityConverter x:Key="BoolToVis" />
        </ResourceDictionary>
    </UserControl.Resources>

    <StackPanel Margin="20">
        <!-- 图标 + 标题行 -->
        <StackPanel Orientation="Horizontal" Margin="0,0,0,15">
            <materialDesign:PackIcon Kind="{Binding IconKind}"
                                     Foreground="{Binding IconForeground}"
                                     Width="28" Height="28" Margin="0,0,10,0"
                                     VerticalAlignment="Center" />
            <TextBlock Text="{Binding Title}" FontSize="22" FontWeight="Bold"
                       Foreground="#424242" VerticalAlignment="Center" />
        </StackPanel>

        <!-- 消息内容 -->
        <TextBlock Text="{Binding Message}" Foreground="#333333"
                   FontSize="14" TextWrapping="Wrap" Margin="0,0,0,20" />

        <!-- 动态按钮区域 -->
        <ItemsControl ItemsSource="{Binding Buttons}">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <UniformGrid Rows="1" />
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
                <DataTemplate DataType="{x:Type models:DialogButton}">
                    <Button Command="{Binding ClickCommand}"
                            Height="40" FontSize="14" Margin="3,0">
                        <Button.Resources>
                            <SolidColorBrush x:Key="ButtonBg" Color="{Binding Background, Converter={StaticResource HexToBrushConverter}}" />
                        </Button.Resources>
                        <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                            <materialDesign:PackIcon Kind="{Binding IconKind}"
                                                     Width="18" Height="18" Margin="0,0,6,0"
                                                     Foreground="{Binding Foreground}"
                                                     VerticalAlignment="Center" />
                            <TextBlock Text="{Binding Text}" Foreground="{Binding Foreground}"
                                       VerticalAlignment="Center" />
                        </StackPanel>
                    </Button>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </StackPanel>
</UserControl>
```

注意：由于 WPF 不支持直接在 Button.Background 上绑定十六进制字符串，需要改用 Style 绑定或 Converter。实际实现时使用更简洁的方式——在 Button 的 Style 中通过 DataTrigger 或直接在 DialogButton 中暴露 Brush 属性。

**最终 XAML（使用 Brush 属性替代字符串）：**

```xml
<UserControl x:Class="ModuleCore.Views.CustomDialog"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:prism="http://prismlibrary.com/"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
             xmlns:lang="clr-namespace:Core.Markup;assembly=Core"
             xmlns:models="clr-namespace:ModuleCore.Models"
             prism:ViewModelLocator.AutoWireViewModel="True"
             Width="480">
    <UserControl.Resources>
        <ResourceDictionary>
            <BooleanToVisibilityConverter x:Key="BoolToVis" />
        </ResourceDictionary>
    </UserControl.Resources>

    <StackPanel Margin="20">
        <!-- 图标 + 标题行 -->
        <StackPanel Orientation="Horizontal" Margin="0,0,0,15">
            <materialDesign:PackIcon Kind="{Binding IconKind}"
                                     Foreground="{Binding IconForeground}"
                                     Width="28" Height="28" Margin="0,0,10,0"
                                     VerticalAlignment="Center" />
            <TextBlock Text="{Binding Title}" FontSize="22" FontWeight="Bold"
                       Foreground="#424242" VerticalAlignment="Center" />
        </StackPanel>

        <!-- 消息内容 -->
        <TextBlock Text="{Binding Message}" Foreground="#333333"
                   FontSize="14" TextWrapping="Wrap" Margin="0,0,0,20" />

        <!-- 动态按钮区域 -->
        <ItemsControl ItemsSource="{Binding Buttons}">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <UniformGrid Rows="1" />
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
                <DataTemplate DataType="{x:Type models:DialogButton}">
                    <Button Command="{Binding ClickCommand}"
                            Background="{Binding BackgroundBrush}"
                            Height="40" FontSize="14" Margin="3,0"
                            Style="{DynamicResource MaterialDesignRaisedButton}">
                        <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                            <materialDesign:PackIcon Kind="{Binding IconKind}"
                                                     Width="18" Height="18" Margin="0,0,6,0"
                                                     Foreground="White"
                                                     VerticalAlignment="Center" />
                            <TextBlock Text="{Binding Text}" Foreground="White"
                                       VerticalAlignment="Center" />
                        </StackPanel>
                    </Button>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </StackPanel>
</UserControl>
```

- [ ] **Step 2: 更新 DialogButton 添加 BackgroundBrush 属性**

在 `DialogButton.cs` 中添加：

```csharp
private string _backgroundHex = "#757575";
/// <summary>按钮背景色（十六进制字符串，如 "#4CAF50"）</summary>
public string BackgroundHex
{
    get => _backgroundHex;
    set
    {
        _backgroundHex = value;
        RaisePropertyChanged(nameof(BackgroundHex));
        RaisePropertyChanged(nameof(BackgroundBrush));
    }
}

/// <summary>背景色 Brush（由 BackgroundHex 自动转换）</summary>
public System.Windows.Media.Brush BackgroundBrush
{
    get
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(BackgroundHex);
            return new System.Windows.Media.SolidColorBrush(color);
        }
        catch { return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray); }
    }
}
```

- [ ] **Step 3: 编写 Code-Behind**

```csharp
using System.Windows.Controls;

namespace ModuleCore.Views
{
    public partial class CustomDialog : UserControl
    {
        public CustomDialog()
        {
            InitializeComponent();
        }
    }
}
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build ModuleCore/ModuleCore.csproj --no-restore -v q`
Expected: 编译通过

---

### Task 4: 注册 Dialog

**Files:**
- 修改: `ModuleCore/ModuleCore.cs`

- [ ] **Step 1: 在 RegisterTypes 中添加注册**

在已有的 `containerRegistry.RegisterDialog<ErrorDialog, ErrorDialogViewModel>();` 行之后添加：

```csharp
containerRegistry.RegisterDialog<CustomDialog, CustomDialogViewModel>();
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build GZQL_MACHINE.sln --no-restore -v q`
Expected: 编译通过

---

### Task 5: 替换 MultiStationPositionEditorViewModel 中的调用

**Files:**
- 修改: `RecipeManagement/ViewModels/MultiStationPositionEditorViewModel.cs`

- [ ] **Step 1: 替换 ShowMessageDialogAsync 方法**

将：
```csharp
private Task<IDialogResult> ShowMessageDialogAsync(DialogParameters parameters)
    => _dialogService.ShowDialogAsync("MessageDialog", parameters);
```

改为：
```csharp
private Task<IDialogResult> ShowCustomDialogAsync(DialogParameters parameters)
    => _dialogService.ShowDialogAsync("CustomDialog", parameters);
```

- [ ] **Step 2: 替换 Replay 方法中的调用**

将原调用：
```csharp
var dialogResult = await ShowMessageDialogAsync(new DialogParameters
{
    { "title", Loc("MultiStationPos_ConfirmGotoTitle", "确认前往") },
    { "message", _localization.GetResource("MultiStationPos_ConfirmGotoMessage", positionName) },
    { "yesButtonText", btnSingle },
    { "noButtonText", btnSimultaneous },
    { "extraButtonText", btnCancel },
    { "showYesButton", true },
    { "showNoButton", true },
    { "showExtraButton", true },
    { "iconKind", PackIconKind.Target }
});
```

改为：
```csharp
var dialogResult = await ShowCustomDialogAsync(new DialogParameters
{
    { "title", Loc("MultiStationPos_ConfirmGotoTitle", "确认前往") },
    { "message", _localization.GetResource("MultiStationPos_ConfirmGotoMessage", positionName) },
    { "iconKind", PackIconKind.Target },
    { "buttons", new ObservableCollection<DialogButton>
        {
            new DialogButton { Text = btnCancel, BackgroundHex = "#757575", ButtonIndex = 2, IconKind = PackIconKind.Close },
            new DialogButton { Text = btnSimultaneous, BackgroundHex = "#FF9800", ButtonIndex = 1, IconKind = PackIconKind.FastForward },
            new DialogButton { Text = btnSingle, BackgroundHex = "#4CAF50", ButtonIndex = 0, IconKind = PackIconKind.Target }
        }
    }
});
```

- [ ] **Step 3: 搜索并替换所有 ShowMessageDialogAsync 调用**

在整个项目中搜索 `ShowMessageDialogAsync` 和 `"MessageDialog"` 的使用，逐个替换为 CustomDialog 调用。

- [ ] **Step 4: 编译验证**

Run: `dotnet build GZQL_MACHINE.sln --no-restore -v q`
Expected: 编译通过

---

### Task 6: 最终验证

- [ ] **Step 1: 全项目编译**

Run: `dotnet build GZQL_MACHINE.sln --no-restore -v q`
Expected: 0 错误

- [ ] **Step 2: 确认无残留引用**

搜索项目中是否还有 `"MessageDialog"` 的 ShowDialog/ShowDialogAsync 调用。如有，评估是否需要替换。
