# RunSingleStepCommand 改为右键菜单

## 需求

将"运行此步骤"按钮从工具栏移到步骤行的右键上下文菜单中。

## 修改方案

### 1. ProcessSequenceEditorView.xaml

**移除**工具栏中的 RunSingleStepCommand 按钮（第490-497行）。

**在 DataGrid.RowStyle 中添加 ContextMenu**：在已有的 RowStyle 的 `<Style TargetType="DataGridRow">` 中添加 ContextMenu Setter，包含"运行此步骤"菜单项。

```xml
<Setter Property="ContextMenu">
    <Setter.Value>
        <ContextMenu>
            <MenuItem Header="{DynamicResource PSE_RunSingleStep}"
                      Command="{Binding DataContext.RunSingleStepCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}"
                      Icon="{materialDesign:PackIcon Kind=PlayCircleOutline}" />
        </ContextMenu>
    </Setter.Value>
</Setter>
```

**注意**：ContextMenu 内部的绑定需要通过 `RelativeSource AncestorType=ContextMenu` 再配合 `PlacementTarget` 来桥接到 ViewModel。WPF ContextMenu 不在可视化树中，需要特殊绑定方式：

```xml
<MenuItem Header="{DynamicResource PSE_RunSingleStep}"
          Command="{Binding PlacementTarget.DataContext.RunSingleStepCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}"
          Icon="{materialDesign:PackIcon Kind=PlayCircleOutline}" />
```

### 2. 无需修改其他文件

ViewModel 中的 `RunSingleStepCommand` 保持不变，只是绑定方式从按钮改为右键菜单。

## 涉及文件

| 文件 | 修改内容 |
|------|----------|
| `Module/Controls/StepEditor/ProcessSequenceEditorView.xaml` | 移除工具栏按钮，在RowStyle中添加ContextMenu |
