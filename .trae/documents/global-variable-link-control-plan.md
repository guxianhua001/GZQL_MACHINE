# 全局变量链接用户控件实施计划

## 一、背景与目标

项目中 **8 个模块** 使用全局变量链接功能，实现方式各不相同（IsXxxLinked 判断逻辑不统一、有的缺 UnlinkCommand、有的不过滤非 Double 变量），导致行为不一致且维护困难。

**目标**：创建一个可复用的 `GlobalVariableLinkControl` 用户控件，封装 VisionCapture 中最完善的链接模式，供所有模块统一使用。

**范围**：仅创建控件本身，不修改现有模块（后续逐步迁移）。

---

## 二、控件设计

### 2.1 控件名称与位置

- **控件名**：`GlobalVariableLinkControl`
- **命名空间**：`Module.Controls.Common`
- **文件位置**：
  - `Module/Controls/Common/GlobalVariableLinkControl.xaml`
  - `Module/Controls/Common/GlobalVariableLinkControl.xaml.cs`

### 2.2 控件视觉结构

```
┌─────────────────────────────────────────────────────┐
│ [数值显示 TextBlock] [🔗链接图标 Button] [ComboBox▼] │
└─────────────────────────────────────────────────────┘
```

- **数值显示**：`TextBlock`，绑定 `DisplayValue`，StringFormat=F3，FontWeight=Bold
- **链接图标**：`Button` + `materialDesign:PackIcon Kind="LinkOff"`，颜色由 `IsLinked` 驱动（蓝=#1565C0 / 灰=#BDBDBD）
- **ComboBox**：`IsEditable=True`，`IsTextSearchEnabled=True`，绑定 `LinkableGlobalVariables`

### 2.3 依赖属性（DependencyProperty）

| 属性名 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `DisplayValue` | `double` | `0` | 数值显示（绑定到 ViewModel 的值属性） |
| `DisplayFormat` | `string` | `"F3"` | 数值格式化字符串 |
| `DisplayForeground` | `Brush` | `#E53935` | 数值显示颜色 |
| `IsLinked` | `bool` | `false` | 链接状态（绑定到 ViewModel 的 IsXxxLinked） |
| `UnlinkCommand` | `ICommand` | `null` | 取消链接命令（绑定到 ViewModel 的 UnlinkXxxCommand） |
| `LinkedVariableName` | `string` | `null` | 当前链接的变量名（双向绑定到 ViewModel 的 XxxLinkedVar） |
| `LinkableGlobalVariables` | `ObservableCollection<GlobalVariable>` | `null` | 可链接的全局变量列表（绑定到 ViewModel 的 LinkableGlobalVariables） |
| `ComboBoxWidth` | `double` | `100` | ComboBox 宽度（不同布局需要不同宽度） |
| `HintText` | `string` | `""` | ComboBox 提示文本 |

### 2.4 XAML 默认样式

```xml
<UserControl x:Class="Module.Controls.Common.GlobalVariableLinkControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
             xmlns:lang="clr-namespace:Core.Markup;assembly=Core">
    <UserControl.Resources>
        <converters:BooleanToBrushConverter x:Key="LinkedToBrushConverter"
                                            TrueBrush="#1565C0" FalseBrush="#BDBDBD" />
    </UserControl.Resources>
    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
        <!-- 数值显示 -->
        <TextBlock Text="{Binding DisplayValue, RelativeSource={RelativeSource AncestorType=UserControl}, StringFormat={Binding DisplayFormat, RelativeSource={RelativeSource AncestorType=UserControl}}}"
                   FontWeight="Bold"
                   Foreground="{Binding DisplayForeground, RelativeSource={RelativeSource AncestorType=UserControl}}"
                   FontSize="11" VerticalAlignment="Center" Margin="0,0,2,0" />
        <!-- 链接图标按钮 -->
        <Button Command="{Binding UnlinkCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                Style="{StaticResource MaterialDesignIconButton}"
                Padding="0" Width="16" Height="16" VerticalAlignment="Center"
                ToolTip="{lang:Lang VisionCapture_UnlinkGlobalVariable}" Margin="2,0,0,0">
            <materialDesign:PackIcon Kind="LinkOff" Width="10" Height="10"
                                     Foreground="{Binding IsLinked, RelativeSource={RelativeSource AncestorType=UserControl}, Converter={StaticResource LinkedToBrushConverter}}"
                                     VerticalAlignment="Center" />
        </Button>
        <!-- 全局变量选择 ComboBox -->
        <ComboBox ItemsSource="{Binding LinkableGlobalVariables, RelativeSource={RelativeSource AncestorType=UserControl}}"
                  SelectedValuePath="Name" DisplayMemberPath="Name"
                  SelectedValue="{Binding LinkedVariableName, RelativeSource={RelativeSource AncestorType=UserControl}, UpdateSourceTrigger=LostFocus}"
                  IsEditable="True" IsTextSearchEnabled="True"
                  Width="{Binding ComboBoxWidth, RelativeSource={RelativeSource AncestorType=UserControl}}"
                  FontSize="9" Margin="2,0,0,0" VerticalAlignment="Center"
                  materialDesign:HintAssist.Hint="{Binding HintText, RelativeSource={RelativeSource AncestorType=UserControl}}" />
    </StackPanel>
</UserControl>
```

### 2.5 code-behind

```csharp
public partial class GlobalVariableLinkControl : UserControl
{
    // 9 个 DependencyProperty 声明
    // DisplayValue, DisplayFormat, DisplayForeground,
    // IsLinked, UnlinkCommand, LinkedVariableName,
    // LinkableGlobalVariables, ComboBoxWidth, HintText
}
```

---

## 三、实施步骤

### 步骤 1：创建 `GlobalVariableLinkControl.xaml` + `.xaml.cs`

- 位置：`Module/Controls/Common/`
- 定义 9 个 DependencyProperty
- XAML 布局：TextBlock + Button(LinkOff) + ComboBox
- 内嵌 `BooleanToBrushConverter`（TrueBrush=#1565C0, FalseBrush=#BDBDBD）

### 步骤 2：构建验证

- `dotnet build Module.csproj` 确认 0 错误

---

## 四、使用示例（供后续迁移参考，本次不实施）

```xml
<!-- 替换前（VisionCaptureView 中的 8 行 XAML） -->
<TextBlock Text="{Binding NeedleOffsetX, StringFormat=F3}" FontWeight="Bold" Foreground="#E53935" ... />
<Button Command="{Binding UnlinkNeedleOffsetXCommand}" ...>
    <materialDesign:PackIcon Kind="LinkOff" Foreground="{Binding IsNeedleOffsetXLinked, Converter=...}" />
</Button>
<ComboBox ItemsSource="{Binding LinkableGlobalVariables}" SelectedValue="{Binding NeedleOffsetXLinkedVar, ...}" ... />

<!-- 替换后（1 行） -->
<common:GlobalVariableLinkControl
    DisplayValue="{Binding NeedleOffsetX}"
    DisplayForeground="#E53935"
    IsLinked="{Binding IsNeedleOffsetXLinked}"
    UnlinkCommand="{Binding UnlinkNeedleOffsetXCommand}"
    LinkedVariableName="{Binding NeedleOffsetXLinkedVar, UpdateSourceTrigger=LostFocus}"
    LinkableGlobalVariables="{Binding LinkableGlobalVariables}"
    ComboBoxWidth="80" />
```

---

## 五、设计原则

1. **纯 UI 控件**：不包含业务逻辑，所有状态由外部 ViewModel 通过 DependencyProperty 传入
2. **不依赖特定 ViewModel**：控件不知道 VisionCaptureViewModel 等具体类型
3. **依赖方向正确**：控件 → Framework/Core（仅用 GlobalVariable 模型 + BooleanToBrushConverter），不反向依赖
4. **可定制外观**：DisplayForeground、ComboBoxWidth、HintText 等均可外部配置
5. **多语言支持**：HintText 使用 LangExtension 或外部传入
6. **控件内嵌转换器**：BooleanToBrushConverter 在控件 Resource 内定义，不污染外部资源
