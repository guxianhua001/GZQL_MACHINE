# AxisSettingView 迁移至 MotionControl 模块 — 实施计划

## 一、现状分析

### 1.1 当前文件位置
| 文件 | 当前位置 | 命名空间 |
|------|----------|----------|
| **View** | `ModuleCore\AxisSettingView.xaml` / `.xaml.cs` | `ModuleCore.Views.AxisSettingView` |
| **ViewModel** | `ModuleCore\AxisSettingViewModel.cs` | `AxisConfiguration.ViewModels.AxisSettingViewModel` |
| **Service (IAxisConfigService)** | `ModuleCore\Services\AxisConfigService.cs` | `AxisConfiguration.Services.AxisConfigService` |
| **Models** | `Interfaces\Axis\AxisConfiguration.cs` | `AxisConfiguration.Models.*` |
| **接口定义** | `Interfaces\Axis\IAxisConfigService.cs` | `Interfaces.IAxisConfigService` |

### 1.2 依赖关系
```
AxisSettingView (ModuleCore)
  └─> AxisSettingViewModel (ModuleCore)
       ├─> IAxisConfigService (Interfaces)
       │    └─> AxisConfigService (ModuleCore) ← 直接调用 LTDMC 雷赛SDK
       ├─> Models: AxisInfo, AxisParams, EmergencyStopConfig, HomingConfig, MotionConfig, InterpolationSystem, InterpolationParams, MappedIO, CardInfo, LogicLevel, AxisInSystem (Interfaces)
       ├─> IProgressReporter (Interfaces)
       └─> ProgressDialog (内嵌在 ViewModel 文件中)
```

### 1.3 注册位置
- **ModuleCore.ModuleCore.cs** 第48行：`containerRegistry.RegisterForNavigation<AxisSettingView, AxisSettingViewModel>();`

### 1.4 现有 MotionControl 模块架构
```
MotionControl/
├── Card/                          # 运动卡抽象层（已支持多卡）
│   ├── MotionCardBase.cs          # 抽象基类，定义所有 IMotionCard 方法
│   ├── MotionCardFactory.cs       # 工厂模式，按索引创建/缓存运动卡
│   ├── VirtualMotionCard.cs       # 虚拟卡实现（无硬件调试）
│   ├── Leisai/
│   │   ├── LeisaiMotionCard.cs    # 雷赛卡具体实现 (531行)
│   │   ├── Leisai_Define.cs
│   │   └── LTDMC.cs               # 雷赛SDK P/Invoke 封装
│   └── MotionConvert.cs
├── Interfaces/
│   ├── IMotionCard.cs             # 统一运动卡接口
│   ├── IMotionCardFactory.cs      # 工厂接口
│   └── ...                        # 其他接口
├── Services/
│   ├── MotionService.cs           # 核心运动服务
│   ├── AxisConfigurationService.cs# 轴配置读取（从 hwcfg.xml）
│   └── ...
├── ViewModels/
│   ├── SingleAxisViewModel.cs     # 单轴控制 ViewModel
│   └── ...
├── Views/
│   ├── SingleAxisControlView.xaml # 单轴控制视图（MaterialDesign Card 风格）
│   └── ...
└── MotionControlModule.cs         # 模块注册
```

### 1.5 关键问题
1. **AxisConfigService 直接调用 LTDMC SDK**，未通过 IMotionCard 抽象层 → 不支持多种运动控制卡
2. **ProgressDialog 内嵌在 ViewModel 中** → 应提取为独立组件
3. **命名空间混乱**：`AxisConfiguration.Models`、`AxisConfiguration.ViewModels`、`AxisConfiguration.Services` 散布在不同项目
4. **UI 布局较旧**：固定高度 `Height="800"`，使用传统 GroupBox 布局

---

## 二、目标架构设计

### 2.1 新文件结构
```
MotionControl/
├── Views/
│   └── AxisSettingView.xaml        # 迁移后的轴参数设置视图（重构UI）
│   └── AxisSettingView.xaml.cs
├── ViewModels/
│   └── AxisSettingViewModel.cs     # 重构后的 ViewModel
├── Services/
│   └── AxisParameterService.cs     # 替代原 AxisConfigService，基于 IMotionCard 抽象
├── Models/                         # （可选）新增 MotionControl 专属模型
│   └── AxisParameterGroup.cs       # 参数分组辅助类
├── Converters/
│   └── CardTypeToDescriptionConverter.cs  # 卡类型→描述转换器
└── Dialogs/
    └── ParameterProgressDialog.xaml  # 提取出的进度对话框
```

### 2.2 多卡支持策略
```
                    ┌─────────────────────┐
                    │  IMotionCardFactory  │
                    │   (GetCard(index))   │
                    └──────────┬──────────┘
                               │
              ┌────────────────┼────────────────┐
              ▼                ▼                ▼
     ┌────────────┐   ┌────────────┐   ┌────────────┐
     │ LeisaiCard  │   │ VirtualCard│   │ FutureCard │  ← 可扩展
     │  (index=0)  │   │ (调试模式)  │   │  (固高/...) │
     └────────────┘   └────────────┘   └────────────┘
              │                │                │
              └────────────────┼────────────────┘
                               ▼
              ┌──────────────────────────────────┐
              │    AxisParameterService           │
              │  (通过 IMotionCard 接口操作)       │
              └──────────────────────────────────┘
```

### 2.3 UI 设计方向
- **工业控制风格**：深色主题 + 高对比度状态指示 + MaterialDesign Card 容器
- **左侧导航 + 右侧内容**布局保持不变（已被验证有效）
- **顶部增加控制卡选择器**（多卡支持的核心UI入口）
- **使用 MaterialDesign Card 替代传统 GroupBox**
- **响应式布局**：移除固定 Height，改用 MinHeight + ScrollViewer
- **PackIcon 图标**：所有按钮使用 `<materialDesign:PackIcon>` 替代文字按钮

---

## 三、详细实施步骤

### 步骤 1：创建 AxisParameterService（替代 AxisConfigService）

**目标**：将直接调用 LTDMC 的代码改为通过 `IMotionCard` 接口调用，支持多种运动控制卡。

**新文件**: `MotionControl\Services\AxisParameterService.cs`

```csharp
// 核心职责：
// 1. 从 hwcfg.xml 加载轴定义（复用现有 HardwareConfigParser 逻辑）
// 2. 通过 IMotionCard 接口读写参数（而非直接调 LTDMC）
// 3. JSON 配置文件的保存/加载
// 4. 插补系配置管理

public interface IAxisParameterService
{
    IReadOnlyList<AxisInfo> LoadAllAxes();
    Task DownloadSingleAxisAsync(AxisInfo axis);
    Task DownloadAllParametersAsync(IProgressReporter reporter);
    void SaveAxisParameters(AxisInfo axis);
    void UploadParameters(AxisInfo axis);
    IEnumerable<InterpolationSystem> LoadInterpolationSystems();
    // ... 其他方法
}
```

**关键改动点**：
- `SetEmergencyStop()` → 通过 `card.SetDo()/card.GetDi()` 或扩展 IMotionCard 接口添加急停设置方法
- `SetHomingParameters()` → 通过 `card.SetHomeMode()` 
- `SetMotionParameters()` → 需要在 IMotionCard 上扩展 `SetProfile()` 方法
- `ApplyInterpolationParameters()` → 通过 `card.SetVectorProfileUnit()` 等

**注意**：当前 `IMotionCard` 接口缺少部分细粒度参数设置方法（如 `dmc_set_equiv`, `dmc_set_emg_mode`, `dmc_set_axis_io_map`, `nmc_set_home_profile`, `dmc_set_profile_unit`, `dmc_set_s_profile`, `dmc_set_dec_stop_time`）。需要：
- 在 `IMotionCard` 中添加这些方法签名
- 在 `MotionCardBase` 中声明为 abstract
- 在 `LeisaiMotionCard` 中实现
- 在 `VirtualMotionCard` 中提供空实现或模拟

### 步骤 2：扩展 IMotionCard 接口

**修改文件**: `MotionControl\Interfaces\IMotionCard.cs`

新增方法：
```csharp
// 轴参数设置
int SetPulseEquivalent(int axisId, double pulsePerUnit);
int SetEmergencyStopMode(int axisId, bool enabled, int logicLevel);
int SetAxisIOMap(int axisId, int ioType, int mapIoType, int mapIoIndex, double filterTime);
int SetHomeProfile(int axisId, int mode, double lowSpeed, double highSpeed, double accTime, double decTime, double offset);
int SetProfileUnit(int axisId, double startVel, double maxVel, double accTime, double decTime, double stopVel);
int SetSProfile(int axisId, int reserved, double sPara);
int SetDecStopTime(int axisId, double decStopTime);

// 插补系参数设置（已有部分，确认完整性）
```

同步更新：
- `MotionCardBase.cs` — 添加 abstract 方法
- `LeisaiMotionCard.cs` — 添加雷赛 SDK 实现
- `VirtualMotionCard.cs` — 添加空实现（return 0）

### 步骤 3：迁移并重构 ViewModel

**新文件**: `MotionControl\ViewModels\AxisSettingViewModel.cs`

**改动要点**：
| 项目 | 原来 | 改为 |
|------|------|------|
| 命名空间 | `AxisConfiguration.ViewModels` | `MotionControl.ViewModels` |
| 构造函数依赖 | `IAxisConfigService` | `IAxisParameterService` + `IMotionCardFactory` |
| ProgressDialog | 内嵌类 | 使用独立的 `ParameterProgressDialog` |
| 卡选择 | 仅显示 CardId 列表 | 通过 `IMotionCardFactory.CardCount` 动态获取 |
| MessageBox | 直接调用 | 可选：改为 INotificationService |

**保留不变的功能**：
- 单轴/插补系双模式切换
- 参数变更检测 (`ParametersChanged`)
- 导入/导出 JSON
- 从文件加载/保存参数
- 所有命令（Upload/Download/Save/Load 等）

### 步骤 4：重构 View（UI 设计）

**新文件**: `MotionControl\Views\AxisSettingView.xaml`

**UI 布局规划**：

```
┌──────────────────────────────────────────────────────────────────┐
│  ╔═══════════════════╗  ═══════════════════════════════════════  │
│  ║  🎛️ 轴参数设置    ║  [📋 从卡读取] [⬇️ 设置到卡]  卡:[0 ▾]   │
│  ╠═══════════════════╣  ═══════════════════════════════════════  │
│  ║ 管理单元          ║                                          │
│  ╠──────────────────╣  ┌────────────────┬──────────────────────┐ │
│  ║ [单轴] [插补系]   ║  │ 脉冲当量设置   │ 急停设置             │ │
│  ╠═══════════════════╣  │ ┌───────────┐ │ ☑ 启用急停          │ │
│  ║                   ║  │ │ pulse/unit│ │ 有效电平: [低电平 ▾] │ │
│  ║ 📌 X轴            ║  │ └───────────┘ │ IO映射: [DI-0    ▾] │ │
│  ║ 📌 Y轴            ║  │ cmd: dmc_set_ │                      │ │
│  ║ 📌 Z轴            ║  ├────────────────┤                      │ │
│  ║ 📌 Rz轴           ║  │ 回零设置       │ 运动参数             │ │
│  ║                   ║  │ 低速/高速/...  │ 起始速度/最大速度... │ │
│  ╚═══════════════════╝  └────────────────┴──────────────────────┘ │
│                                                                  │
│  ═══════════════════════════════════════════════════════════════  │
│  [📤 导出] [📥 导入]          [✅ 设置全部] [📂 加载] [💾 保存]  │
└──────────────────────────────────────────────────────────────────┘
```

**设计规范**：
- 使用 `<materialDesign:Card>` 作为主容器（与 SingleAxisControlView 风格统一）
- 左侧面板：深色主题头 + TabControl + ListBox（保持原有结构）
- 右侧面板：2列 Grid 包裹多个 Card（替代旧 GroupBox）
- 顶部工具栏：卡片选择 ComboBox + 操作按钮
- 底部操作栏：导出/导入 + 批量操作
- 所有文本使用 `{lang:Lang}` 多语言绑定
- 按钮使用 PackIcon + 文字组合
- 移除 `Height="800"` 固定高度，改用自适应

### 步骤 5：提取 ParameterProgressDialog

**新文件**: `MotionControl\Dialogs\ParameterProgressDialog.xaml` + `.xaml.cs`

- 从 `AxisSettingViewModel.cs` 中提取 `ProgressDialog` 类
- 改为独立 XAML 视图，支持多语言
- 实现 `IProgressReporter` 接口
- 在 `MotionControlModule.cs` 中注册为 Dialog

### 步骤 6：注册到 MotionControlModule

**修改文件**: `MotionControl\MotionControlModule.cs`

```csharp
// 新增注册
containerRegistry.RegisterSingleton<IAxisParameterService, AxisParameterService>();
containerRegistry.RegisterForNavigation<AxisSettingView, AxisSettingViewModel>();
containerRegistry.RegisterDialog<ParameterProgressDialog>();
```

### 步骤 7：从 ModuleCore 移除旧文件

**删除/清理**：
1. `ModuleCore\AxisSettingView.xaml` → 删除
2. `ModuleCore\AxisSettingView.xaml.cs` → 删除（如果存在）
3. `ModuleCore\AxisSettingViewModel.cs` → 删除
4. `ModuleCore\ModuleCore.cs` → 移除第48行的 `RegisterForNavigation<AxisSettingView, AxisSettingViewModel>`
5. `ModuleCore\Services\AxisConfigService.cs` → **保留但标记 `[Obsolete]`**（其他地方可能引用）

### 步骤 8：更新导航引用

搜索整个项目中引用 `AxisSettingView` 的位置（Region 导航等），确保指向新的 MotionControl 模块注册。

### 步骤 9：多语言支持

- 复用已有的 `AxisSetting_*` 前缀 key（约42个，已在 zh-CN/en-US 中定义）
- 新增 UI 重构引入的新 key（如 `AxisSetting_CardSelector`、`AxisSetting_CardType` 等）
- 确保 XAML 中所有用户可见文本都使用 `{lang:Lang}` 绑定

### 步骤 10：编译验证

```bash
dotnet build --no-restore
```
确认：
- 无编译错误
- 无命名空间冲突
- PRISM 导航正常工作
- 多语言资源完整

---

## 四、Interfaces 项目清理（删除不使用的运动控制接口）

### 4.1 审计结论

经过全项目搜索，Interfaces 中运动控制相关内容的使用情况如下：

| 文件/接口 | 外部引用位置（排除即将迁移的 ModuleCore 文件） | 结论 |
|-----------|------|------|
| **`Interfaces\Axis\IAxisConfigService.cs`** | `MainApp\App.xaml.cs:134` — 注册<br>`ModuleCore\MainWindowViewModel.cs:80,90,122` — **注入但从未调用任何方法（死代码）**<br>`ModuleCore\AxisSettingViewModel.cs` — 即将迁移到 MotionControl<br>`ModuleCore\Services\AxisConfigService.cs` — 实现（即将废弃） | ✅ **可安全删除** |
| **`Interfaces\Axis\AxisConfiguration.cs`** (模型类) | `AxisInfo`, `AxisParams`, `InterpolationSystem` 等 — 新的 `IAxisParameterService` 仍需使用这些模型 | ❌ **保留不动** |
| **`Interfaces\SharedInterfaces\IProgressReporter.cs`** | `MotionControl` + `Core` 模块正在使用 | ❌ **保留不动** |

### 4.2 删除清单

#### 可删除（1个文件）：
- 🗑️ `Interfaces\Axis\IAxisConfigService.cs` — 整个文件删除

#### 需要同步清理的引用：
| 文件 | 清理操作 |
|------|----------|
| `MainApp\App.xaml.cs:134` | 移除 `containerRegistry.RegisterSingleton<IAxisConfigService, AxisConfigService>();` 和对应的 `using AxisConfiguration.Services;` |
| `ModuleCore\ViewModels\MainWindowViewModel.cs:80,90,122` | 移除 `_configService` 字段、构造函数参数、赋值语句 |
| `ModuleCore\Services\AxisConfigService.cs` | 删除整个文件（实现类随接口一起废弃） |

### 4.3 保留的模型类说明

`Interfaces\Axis\AxisConfiguration.cs` 中的以下类型**继续保留**，供新的 `MotionControl.Services.AxisParameterService` 使用：

```
LogicLevel          — 枚举：电平高低
CardInfo            — 控制卡信息
AxisInSystem        — 插补系中的轴
AxisInfo            — 轴信息（核心模型）
EmergencyStopConfig — 急停配置
MappedIO            — IO映射
HomingConfig        — 回零参数
MotionConfig        — 运动参数
AxisParams          — 轴参数聚合体
InterpolationSystem  — 插补系
InterpolationParams  — 插补系运动参数
```

---

## 五、风险与注意事项

### 5.1 兼容性风险
| 风险 | 缓解措施 |
|------|----------|
| `IAxisConfigService` 被隐式依赖 | 已审计：仅 MainWindowViewModel 死代码引用，可安全删除 |
| LTDMC 特有功能无法抽象 | 在 `LeisaiMotionCard` 中保留完整实现；`IMotionCard` 扩展方法只包含通用子集 |
| `AxisConfiguration.Models` 被广泛使用 | Models 保留在 `Interfaces\Axis\` 中不动，仅迁移 View/ViewModel/Service |

### 5.2 性能考虑
- 参数设置操作保持 `Task.Run()` 异步执行（不阻塞 UI）
- 轴列表加载使用懒加载
- 进度报告使用 `IProgress<T>` 模式

### 5.3 安全性
- "设置到卡"操作前增加二次确认对话框
- 批量设置全部轴时逐轴执行+错误隔离（单轴失败不影响其他轴）

---

## 六、实施顺序总结

| 序号 | 任务 | 涉及文件 | 复杂度 |
|------|------|----------|--------|
| 0 | **删除 Interfaces 中废弃接口** | 删除 `IAxisConfigService.cs`；清理 `App.xaml.cs`, `MainWindowViewModel.cs` 中的死引用 | 低 |
| 1 | 扩展 IMotionCard 接口 + 各实现类 | IMotionCard.cs, MotionCardBase.cs, LeisaiMotionCard.cs, VirtualMotionCard.cs | 中 |
| 2 | 创建 IAxisParameterService + AxisParameterService | 新建 AxisParameterService.cs | 高 |
| 3 | 创建 ParameterProgressDialog | 新建 Dialogs/ParameterProgressDialog.xaml | 低 |
| 4 | 创建 AxisSettingViewModel（重构） | 新建 ViewModels/AxisSettingViewModel.cs | 高 |
| 5 | 创建 AxisSettingView（UI 重构） | 新建 Views/AxisSettingView.xaml | 高 |
| 6 | 更新 MotionControlModule 注册 | MotionControlModule.cs | 低 |
| 7 | 清理 ModuleCore 旧文件 | ModuleCore.cs, 删除 AxisSettingView/ViewModel/AxisConfigService | 低 |
| 8 | 更新导航引用 | 搜索全局引用 | 低 |
| 9 | 补充多语言资源 | Strings.zh-CN.xaml, Strings.en-US.xaml | 低 |
| 10 | 编译验证 + 测试 | 全项目 | 中 |
