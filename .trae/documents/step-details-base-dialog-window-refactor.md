# StepDetails 对话框系统重构计划

## 概述

将 StepDetails 自定义动作对话框从 `MainDialogHost`（MaterialDesign DialogHost）方式改为普通 Window 弹出方式，使用 ModuleCore 中统一的 `BaseDialogWindow` 基础窗口，支持暗色/明亮主题切换。

## 当前状态分析

### 现有架构
- **15 个 StepDetail 视图**（均为 UserControl）：位于 `Module\Controls\StepDetails\`
- **显示方式**：通过 `DialogHost.Show(view, "MainDialogHost")` 模态弹出
- **关闭方式**：各 ViewModel 调用 `DialogHost.GetDialogSession("MainDialogHost").Close(result)`
- **入口**：`ProcessSequenceEditorViewModel.NavigateToDetailView` 分发到 12 个 `ShowXxxDetailDialog` 方法
- **安全显示**：`ShowDialogSafely` 封装了 DialogHost 调用

### 问题
1. DialogHost 是全局模态遮罩，无法独立设置主题
2. DialogHost 遮罩为 Dark，与全局 Light 主题不一致
3. 无运行时主题切换机制
4. ViewModel 直接依赖 `DialogHost` 静态 API，耦合度高

## 设计方案

### 架构设计

```
┌─────────────────────────────────────────────────────┐
│                    ModuleCore                        │
│                                                      │
│  ┌──────────────────┐    ┌────────────────────────┐ │
│  │ BaseDialogWindow  │    │ BaseDialogService      │ │
│  │ (Window 基础窗口)  │◄───│ (IBaseDialogService)   │ │
│  │ - 标题栏          │    │ - ShowDialog()         │ │
│  │ - 主题切换按钮     │    │ - CloseDialog()        │ │
│  │ - ContentPresenter│    │ - 主题管理             │ │
│  │ - 关闭按钮         │    └────────────────────────┘ │
│  └──────────────────┘                               │
│          ▲                                           │
│          │ 实现                                       │
│  ┌──────────────────┐                               │
│  │ BaseDialogWindowVM│                               │
│  │ - Title           │                               │
│  │ - IsDarkTheme     │                               │
│  │ - ToggleThemeCmd  │                               │
│  │ - CloseCmd         │                               │
│  └──────────────────┘                               │
└─────────────────────────────────────────────────────┘
          ▲
          │ 依赖注入
┌─────────┴───────────────────────────────────────────┐
│                    Module                            │
│                                                      │
│  ProcessSequenceEditorViewModel                      │
│  - ShowXxxDetailDialog()                             │
│    → _baseDialogService.ShowDialog(view, title)      │
│                                                      │
│  StepDetailViewModels (15个)                         │
│  - 实现 IDialogCloseable                             │
│  - RequestClose?.Invoke(result) 替代 DialogHost.Close│
└─────────────────────────────────────────────────────┘
          ▲
          │ 定义接口
┌─────────┴───────────────────────────────────────────┐
│                     Core                             │
│                                                      │
│  ┌─────────────────────┐  ┌──────────────────────┐  │
│  │ IDialogCloseable     │  │ IBaseDialogService   │  │
│  │ - RequestClose event │  │ - ShowDialog()       │  │
│  │ - CanCloseDialog     │  │ - CloseDialog()      │  │
│  └─────────────────────┘  └──────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

### 依赖方向
- `Core`：定义 `IDialogCloseable` 和 `IBaseDialogService` 接口（最底层，无依赖）
- `ModuleCore`：实现 `BaseDialogWindow`、`BaseDialogService`（依赖 Core）
- `Module`：使用 `IBaseDialogService`，StepDetail VM 实现 `IDialogCloseable`（依赖 Core + ModuleCore）

**不产生倒置依赖**。

## 具体改动

### 1. Core 层：定义接口

#### 文件：`Core\Abstraction\IDialogCloseable.cs`（新建）
```csharp
namespace Core.Abstraction
{
    /// <summary> 可关闭对话框接口：ViewModel 实现此接口以请求关闭对话框 </summary>
    public interface IDialogCloseable
    {
        /// <summary> 请求关闭对话框时触发，参数为返回结果 </summary>
        event Action<object> RequestClose;

        /// <summary> 是否可以关闭对话框（用于验证） </summary>
        bool CanCloseDialog();
    }
}
```

#### 文件：`Core\Abstraction\IBaseDialogService.cs`（新建）
```csharp
namespace Core.Abstraction
{
    /// <summary> 基础对话框服务接口：统一窗口弹出方式 </summary>
    public interface IBaseDialogService
    {
        /// <summary> 显示对话框（模态），返回关闭时的结果 </summary>
        /// <param name="content">UserControl 内容</param>
        /// <param name="title">窗口标题</param>
        /// <param name="isDarkTheme">是否使用暗色主题</param>
        /// <returns>对话框关闭时的结果对象</returns>
        Task<object> ShowDialog(UserControl content, string title = null, bool isDarkTheme = false);

        /// <summary> 关闭当前活动对话框 </summary>
        /// <param name="result">返回结果</param>
        void CloseDialog(object result = null);
    }
}
```

### 2. ModuleCore 层：实现基础窗口

#### 文件：`ModuleCore\Views\BaseDialogWindow.xaml`（新建）

**设计风格**：工业精密感 + 现代简约
- `WindowStyle="None"` + `AllowsTransparency="True"` + `ResizeMode="NoResize"`
- 圆角边框（CornerRadius=8）
- 自定义标题栏（可拖动）：标题文本 + 主题切换图标 + 关闭按钮
- 阴影效果（DropShadowEffect）
- 平滑打开/关闭动画
- 主题切换动画过渡

**XAML 结构**：
```xml
<Window x:Class="ModuleCore.Views.BaseDialogWindow"
        WindowStyle="None"
        AllowsTransparency="True"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize"
        ShowInTaskbar="False"
        SizeToContent="WidthAndHeight">

    <Window.Resources>
        <!-- 主题资源字典（运行时切换） -->
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml"/>
                <materialDesign:BundledTheme BaseTheme="Light" PrimaryColor="Deeppurple" SecondaryColor="Lime"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Window.Resources>

    <Border x:Name="RootBorder"
            CornerRadius="8"
            Background="{DynamicResource MaterialDesignPaper}"
            BorderBrush="{DynamicResource MaterialDesignDivider}"
            BorderThickness="1">
        <Border.Effect>
            <DropShadowEffect BlurRadius="20" ShadowDepth="0" Opacity="0.3" Color="Black"/>
        </Border.Effect>

        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>  <!-- 标题栏 -->
                <RowDefinition Height="*"/>     <!-- 内容 -->
            </Grid.RowDefinitions>

            <!-- 标题栏 -->
            <Border Grid.Row="0" Height="44"
                    Background="{DynamicResource PrimaryHueMidBrush}"
                    CornerRadius="8,8,0,0"
                    MouseLeftButtonDown="TitleBar_MouseLeftButtonDown">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>

                    <!-- 标题文本 -->
                    <TextBlock Grid.Column="0"
                               Text="{Binding Title}"
                               Foreground="White"
                               VerticalAlignment="Center"
                               Margin="16,0,0,0"
                               FontSize="14"
                               FontWeight="Medium"/>

                    <!-- 主题切换按钮 -->
                    <Button Grid.Column="1"
                            Command="{Binding ToggleThemeCommand}"
                            Style="{StaticResource MaterialDesignIconButton}"
                            Foreground="White"
                            Width="36" Height="36">
                        <materialDesign:PackIcon Kind="{Binding ThemeIconKind}"/>
                    </Button>

                    <!-- 关闭按钮 -->
                    <Button Grid.Column="2"
                            Command="{Binding CloseCommand}"
                            Style="{StaticResource MaterialDesignIconButton}"
                            Foreground="White"
                            Width="36" Height="36">
                        <materialDesign:PackIcon Kind="Close"/>
                    </Button>
                </Grid>
            </Border>

            <!-- 内容区域 -->
            <ContentPresenter Grid.Row="1"
                              Content="{Binding Content}"
                              Margin="0"/>
        </Grid>
    </Border>
</Window>
```

#### 文件：`ModuleCore\Views\BaseDialogWindow.xaml.cs`（新建）
- 标题栏拖动处理
- 主题切换资源字典管理
- 窗口打开/关闭动画

#### 文件：`ModuleCore\ViewModels\BaseDialogWindowViewModel.cs`（新建）
```csharp
public class BaseDialogWindowViewModel : BindableBase
{
    private string _title;
    private object _content;
    private bool _isDarkTheme;
    private PackIconKind _themeIconKind = PackIconKind.WeatherSunny;

    public string Title { get => _title; set => SetProperty(ref _title, value); }
    public object Content { get => _content; set => SetProperty(ref _content, value); }
    public bool IsDarkTheme { get => _isDarkTheme; set { SetProperty(ref _isDarkTheme, value); UpdateThemeIcon(); } }
    public PackIconKind ThemeIconKind { get => _themeIconKind; set => SetProperty(ref _themeIconKind, value); }

    public DelegateCommand ToggleThemeCommand { get; }
    public DelegateCommand CloseCommand { get; }

    public event Action<object> RequestClose;

    public BaseDialogWindowViewModel()
    {
        ToggleThemeCommand = new DelegateCommand(OnToggleTheme);
        CloseCommand = new DelegateCommand(() => RequestClose?.Invoke(null));
    }

    private void OnToggleTheme() => IsDarkTheme = !IsDarkTheme;
    private void UpdateThemeIcon() => ThemeIconKind = IsDarkTheme ? PackIconKind.WeatherNight : PackIconKind.WeatherSunny;
}
```

#### 文件：`ModuleCore\Services\BaseDialogService.cs`（新建）
```csharp
public class BaseDialogService : IBaseDialogService
{
    private BaseDialogWindow _currentWindow;
    private TaskCompletionSource<object> _tcs;

    public Task<object> ShowDialog(UserControl content, string title = null, bool isDarkTheme = false)
    {
        _tcs = new TaskCompletionSource<object>();

        var window = new BaseDialogWindow();
        var vm = new BaseDialogWindowViewModel
        {
            Title = title ?? "",
            Content = content,
            IsDarkTheme = isDarkTheme
        };

        vm.RequestClose += (result) =>
        {
            window.DialogResult = result != null;
            window.Close();
        };

        // 如果内容实现了 IDialogCloseable，订阅其关闭请求
        if (content.DataContext is IDialogCloseable closeable)
        {
            closeable.RequestClose += (result) =>
            {
                window.DialogResult = result != null;
                window.Close();
            };
        }

        window.Closed += (s, e) =>
        {
            _tcs.TrySetResult(window.DialogResult);
            _currentWindow = null;
        };

        _currentWindow = window;
        window.ShowDialog();

        return _tcs.Task;
    }

    public void CloseDialog(object result = null)
    {
        if (_currentWindow != null)
        {
            _currentWindow.DialogResult = result != null;
            _currentWindow.Close();
        }
    }
}
```

#### 文件：`ModuleCore\ModuleCore.cs`（修改）
- 在 `RegisterTypes` 中注册：`containerRegistry.RegisterSingleton<IBaseDialogService, BaseDialogService>();`

### 3. Module 层：修改 ViewModel

#### 文件：`Module\Controls\StepEditor\ProcessSequenceEditorViewModel.cs`（修改）

**改动 1**：注入 `IBaseDialogService`
```csharp
private readonly IBaseDialogService _baseDialogService;

// 构造函数添加参数
public ProcessSequenceEditorViewModel(..., IBaseDialogService baseDialogService)
{
    _baseDialogService = baseDialogService;
    ...
}
```

**改动 2**：替换 `ShowDialogSafely` 方法
```csharp
// 旧方法（删除或保留用于其他 DialogHost 调用）
// private static async Task<object> ShowDialogSafely(object content, string dialogIdentifier = "MainDialogHost")

// 新方法
private async Task ShowStepDetailDialog(UserControl view, string titleKey)
{
    var title = _localization.GetResourceOrDefault(titleKey, titleKey);
    await _baseDialogService.ShowDialog(view, title, isDarkTheme: false);
    await AutoSaveSequenceAsync();
}
```

**改动 3**：修改各 `ShowXxxDetailDialog` 方法
```csharp
// 以 ShowRunTaskDetailDialog 为例
private async void ShowRunTaskDetailDialog(ProcessStep step)
{
    var vm = _containerProvider.Resolve<RunTaskDetailViewModel>();
    var view = new RunTaskDetailView();
    view.DataContext = vm;
    vm.Step = step;
    await ShowStepDetailDialog(view, "PSE_RunTaskAction");
}
```

所有 12 个 `ShowXxxDetailDialog` 方法统一改为调用 `ShowStepDetailDialog`。

#### 文件：`Module\Controls\StepDetails\RunTaskDetailViewModel.cs`（修改，代表所有 15 个 Detail VM）

**改动 1**：实现 `IDialogCloseable`
```csharp
public class RunTaskDetailViewModel : BindableBase, IDialogCloseable
{
    public event Action<object> RequestClose;

    public bool CanCloseDialog() => true;
    ...
}
```

**改动 2**：替换关闭逻辑
```csharp
// 旧代码
private void OnSave()
{
    // ... 保存逻辑
    var session = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession("MainDialogHost");
    session?.Close(true);
}

// 新代码
private void OnSave()
{
    // ... 保存逻辑
    RequestClose?.Invoke(true);
}
```

**需修改的 15 个 ViewModel**（统一模式）：
1. `RunTaskDetailViewModel.cs` - OnSave, OnClose
2. `GotoDetailViewModel.cs` - OnSave, OnClose
3. `VisionDetailViewModel.cs` - OnSave, OnClose
4. `ScanDetailViewModel.cs` - OnSave, OnClose
5. `SeekDetailViewModel.cs` - OnSave, OnClose
6. `WaitDetailViewModel.cs` - OnSave, OnClose
7. `ScriptDetailViewModel.cs` - OnSave, OnClose
8. `PickDetailViewModel.cs` - OnSave, OnClose
9. `ReleaseDetailViewModel.cs` - OnSave, OnClose
10. `CureDetailViewModel.cs` - OnSave, OnClose
11. `DispenseDetailViewModel.cs` - OnSave, OnClose
12. `AlignDetailViewModel.cs` - OnSave, OnClose（如有）
13. `CheckDetailViewModel.cs` - OnSave, OnClose（如有）
14. `ConditionBranchViewModel.cs` - OnSave, OnClose（如有）
15. `DataDashboardViewModel.cs` - OnSave, OnClose（如有）

### 4. 主题切换实现

#### `BaseDialogWindow.xaml.cs` 中的主题切换
```csharp
// 主题资源字典路径
private static readonly Uri LightThemeUri = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml");
private static readonly Uri DarkThemeUri = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml");

// 监听 ViewModel 的 IsDarkTheme 变化
private void OnIsDarkThemeChanged(bool isDark)
{
    // 1. 移除旧主题资源字典
    var oldTheme = isDark ? LightThemeUri : DarkThemeUri;
    var newTheme = isDark ? DarkThemeUri : LightThemeUri;

    for (int i = Resources.MergedDictionaries.Count - 1; i >= 0; i--)
    {
        if (Resources.MergedDictionaries[i].Source == oldTheme)
        {
            Resources.MergedDictionaries[i] = new ResourceDictionary { Source = newTheme };
            break;
        }
    }

    // 2. 更新 BundledTheme BaseTheme
    foreach (var dict in Resources.MergedDictionaries)
    {
        if (dict is MaterialDesignThemes.Wpf.BundledTheme bundled)
        {
            bundled.BaseTheme = isDark ? BaseTheme.Dark : BaseTheme.Light;
        }
    }

    // 3. 更新标题栏背景
    ApplyTitleBarTheme(isDark);
}
```

### 5. 多语言支持

#### 新增本地化键（zh-CN 和 en-US）
| Key | 中文 | English |
|-----|------|---------|
| `PSE_DialogTitleGoto` | 跳转设置 | Goto Settings |
| `PSE_DialogTitleVision` | 视觉检测 | Vision Inspection |
| `PSE_DialogTitleScan` | 扫码设置 | Scan Settings |
| `PSE_DialogTitleSeek` | 寻找设置 | Seek Settings |
| `PSE_DialogTitleWait` | 等待设置 | Wait Settings |
| `PSE_DialogTitleScript` | 脚本设置 | Script Settings |
| `PSE_DialogTitlePick` | 取料设置 | Pick Settings |
| `PSE_DialogTitleRelease` | 放料设置 | Release Settings |
| `PSE_DialogTitleCure` | 固化设置 | Cure Settings |
| `PSE_DialogTitleDispense` | 点胶设置 | Dispense Settings |
| `PSE_DialogTitleRunTask` | 调用任务 | Run Task |
| `PSE_DialogTitleAlign` | 对位设置 | Align Settings |
| `PSE_DialogTitleCheck` | 检测设置 | Check Settings |
| `PSE_DialogTitleBranch` | 条件分支 | Condition Branch |
| `PSE_DialogTitleDashboard` | 数据看板 | Data Dashboard |

## 假设与决策

### 假设
1. `MainDialogHost` 仍保留用于其他模块（Recipe、Alarm 等），仅 StepDetails 迁移到新方案
2. StepDetail 视图（UserControl）内容不变，仅修改显示方式和关闭逻辑
3. 主题切换为窗口级别（每个对话框独立切换），不影响全局主题
4. 对话框默认使用明亮主题，用户可手动切换到暗色

### 决策
1. **接口放 Core 层**：`IDialogCloseable` 和 `IBaseDialogService` 放在 `Core\Abstraction\`，避免倒置依赖
2. **实现放 ModuleCore 层**：`BaseDialogWindow` 和 `BaseDialogService` 放在 `ModuleCore\`
3. **不使用 Prism IDialogService**：保持简单，自定义 `IBaseDialogService` 更灵活
4. **窗口模式**：`ShowDialog()` 模态弹出，`SizeToContent="WidthAndHeight"` 自适应内容大小
5. **主题切换方式**：ResourceDictionary 替换 + BundledTheme BaseTheme 切换

## 验证步骤

1. **构建验证**：`dotnet build` 全部项目无错误
2. **功能验证**：
   - 打开任意步骤详情（如 GOTO），确认以独立窗口弹出
   - 窗口标题正确显示对应步骤名称
   - 点击保存/关闭按钮，窗口正确关闭并自动保存
   - 点击主题切换按钮，窗口在暗色/明亮之间切换
   - 标题栏可拖动移动窗口
3. **回归验证**：
   - 其他使用 MainDialogHost 的功能（Recipe、Alarm）仍正常工作
   - ProcessSequenceEditor 树形结构、右键菜单等功能不受影响
4. **多语言验证**：切换语言后，窗口标题正确显示对应语言
5. **架构验证**：无倒置依赖，Core 层不依赖 ModuleCore 或 Module

## 实施顺序

1. Core 层：创建 `IDialogCloseable` 和 `IBaseDialogService` 接口
2. ModuleCore 层：创建 `BaseDialogWindow` + `BaseDialogWindowViewModel` + `BaseDialogService`
3. ModuleCore 层：注册 DI
4. Module 层：修改 `ProcessSequenceEditorViewModel`（注入服务、替换显示方法）
5. Module 层：修改 15 个 StepDetail ViewModel（实现 `IDialogCloseable`、替换关闭逻辑）
6. 语言文件：添加对话框标题本地化键
7. 构建验证 + 功能测试
