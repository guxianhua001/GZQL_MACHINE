# DXF 导入一致性修复计划

## 问题描述
Step1ImportPanel 和 CadAlignmentView 导入同一个 DXF 文件后显示结果不一致：
- Step1ImportPanel：显示完整图形（包含圆弧）
- CadAlignmentView：缺失圆弧，图形不完整

## 根本原因
两个 ViewModel 使用了不同的 `DxfImportOptions` 配置：
- **CadPointEditorViewModel**: `ForDispenseEditor` (IncludeArcs=true, Discretize=1mm)
- **CadAlignmentViewModel**: `ForAlignment` (IncludeArcs=false, Discretize=0)

## 修复方案

### 步骤 1：统一导入选项配置
**文件**: `Core/Services/IDxfImportHelper.cs`

修改 `DxfImportOptions.ForAlignment` 配置：
```csharp
public static DxfImportOptions ForAlignment => new DxfImportOptions
{
    IncludeArcs = true,        // ✅ 修改：包含圆弧（与 Step1ImportPanel 一致）
    IncludeCircles = true,
    IncludeSplines = true,
    DiscretizePitchMM = 1.0,   // ✅ 修改：启用离散化（保证渲染一致性）
    ExtractPoints = true       // 保持：提取点位用于 DataGrid
};
```

### 步骤 2：验证 CadAlignmentViewModel 渲染路径
**文件**: `Module/Controls/Assembly/CadAlignmentViewModel.cs`

确认以下代码路径正确：
1. `_dxfImportHelper.Import()` 返回的 `DisplayEntities` 包含 ARC 实体
2. `CadEntities` 集合正确绑定到 HalconCanvasControl
3. `RenderEntities()` 方法能正确处理所有实体类型（包括新增的 SPLINE）

### 步骤 3：编译验证
运行编译命令确认无错误：
```bash
dotnet build GZQL_MACHINE.sln --no-restore
```

### 步骤 4：功能测试
测试场景：
1. 在 Step1ImportPanel 导入 DXF → 验证圆弧正常显示
2. 在 CadAlignmentView 导入同一个 DXF → 验证显示结果与 Step1 一致
3. 确认 CadAlignmentView 的点位提取功能正常（DataGrid 显示）

## 预期效果
修复后两个视图应该显示**完全相同**的 CAD 图形（除了 CadAlignmentView 额外显示点位列表）。

## 影响范围
- 仅修改 `DxfImportOptions.ForAlignment` 静态配置
- 不影响其他使用 `ForDispenseEditor` 的地方
- 向后兼容：CadAlignmentView 现在能显示完整的 CAD 图形
