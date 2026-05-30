# 重命名 Halcon 项目 & 移除 VM.Halcon 遗留项目

## 背景分析

### 当前状况
项目中存在两个功能完全相同的 Halcon 封装库：

| 项目 | 目标框架 | 命名空间 | 输出 DLL | 被引用方 |
|------|---------|---------|---------|---------|
| `Halcon` | .NET 9.0 | `Halcon.*` | **Halcon.dll** | Module, Core |
| `VM.Halcon` | .NET Framework 4.7.2 | `VM.Halcon.*` | VM.Halcon.dll | MainApp |

### 核心问题
1. **Halcon 项目输出的 `Halcon.dll` 与 MVTec 官方的原生 `halcon.dll` 重名**，虽然大小写不同（Windows 文件系统不区分大小写！），实际运行时可能产生 DLL 加载冲突，这很可能是"初始化 Halcon 窗口错误"的根因之一。
2. `VM.Halcon` 是 .NET Framework 4.7.2 的遗留项目，MainApp（.NET 9.0）引用它存在框架兼容性问题。
3. 两个项目代码完全克隆，造成维护负担。

### 命名方案

推荐将 `Halcon` 项目重命名为 `HalconWrapper`：
- **避免与原生 `halcon.dll` 重名**：输出 DLL 变为 `HalconWrapper.dll`，不再与 `halcon.dll` 冲突
- **语义清晰**：表明这是对 Halcon 的封装层（Wrapper）
- **命名空间同步变更**：`Halcon` → `HalconWrapper`，`Halcon.Model` → `HalconWrapper.Model`，`Halcon.Config` → `HalconWrapper.Config`，`Halcon.Helper` → `HalconWrapper.Helper`

### 是否能解决初始化 Halcon 窗口错误？

**有可能缓解，但不能保证完全解决。** 原因：
- 重命名后消除了 `Halcon.dll` 与 `halcon.dll` 的文件名冲突（Windows 不区分大小写），减少运行时 DLL 搜索歧义
- 但初始化窗口错误的更常见原因是原生 `halcon.dll` 不在程序搜索路径中，或 `halcondotnet.dll` 版本不匹配
- 之前已添加的设计时保护（`DesignerProperties.GetIsInDesignMode`）已解决了设计器中的错误

---

## 实施步骤

### 步骤 1：重命名 Halcon 项目为 HalconWrapper

1.1 修改 `Halcon/Halcon.csproj`：
- `<RootNamespace>Halcon</RootNamespace>` → `<RootNamespace>HalconWrapper</RootNamespace>`
- `<AssemblyName>Halcon</AssemblyName>` → `<AssemblyName>HalconWrapper</AssemblyName>`

1.2 将 `Halcon/` 目录重命名为 `HalconWrapper/`

1.3 更新 `HalconWrapper/` 下所有 .cs 文件的命名空间：
- `namespace Halcon` → `namespace HalconWrapper`
- `namespace Halcon.Model` → `namespace HalconWrapper.Model`
- `namespace Halcon.Config` → `namespace HalconWrapper.Config`
- `namespace Halcon.Helper` → `namespace HalconWrapper.Helper`
- `namespace Halcon.Properties` → `namespace HalconWrapper.Properties`
- 所有 `using Halcon;` → `using HalconWrapper;`
- 所有 `using Halcon.Model;` → `using HalconWrapper.Model;`
- 所有 `using Halcon.Config;` → `using HalconWrapper.Config;`
- 所有 `using Halcon.Helper;` → `using HalconWrapper.Helper;`

### 步骤 2：更新解决方案文件

修改 `GZQL_MACHINE.sln`：
- 更新 Halcon 项目的路径引用：`Halcon\Halcon.csproj` → `HalconWrapper\HalconWrapper.csproj`

### 步骤 3：更新所有引用 Halcon 项目的 .csproj 文件

3.1 `Module/Module.csproj`：
- `<ProjectReference Include="..\Halcon\Halcon.csproj" />` → `<ProjectReference Include="..\HalconWrapper\HalconWrapper.csproj" />`

3.2 `Core/Core.csproj`：
- `<ProjectReference Include="..\Halcon\Halcon.csproj" />` → `<ProjectReference Include="..\HalconWrapper\HalconWrapper.csproj" />`

### 步骤 4：更新所有外部项目中对 Halcon 命名空间的引用

4.1 `Module/Controls/HalconCanvasControl.xaml.cs`：
- `using Halcon;` → `using HalconWrapper;`

4.2 `Core/Models/CadEntityHalconExtensions.cs`：
- `using HalconDotNet;` 保持不变（这是 halcondotnet 的命名空间，不是 Halcon 项目的）

4.3 搜索所有 Module 和 Core 项目中的 `using Halcon;` / `using Halcon.Model;` / `using Halcon.Config;` / `using Halcon.Helper;` 并替换

### 步骤 5：删除 VM.Halcon 项目

5.1 从 `GZQL_MACHINE.sln` 中移除 VM.Halcon 项目条目

5.2 从 `MainApp/MainApp.csproj` 中移除：
- `<ProjectReference Include="..\VM.Halcon\VM.Halcon.csproj" />`

5.3 删除 `VM.Halcon/` 整个目录

### 步骤 6：验证与清理

6.1 执行 `dotnet build` 确保编译通过

6.2 检查是否有遗漏的引用（搜索 "VM.Halcon" 和旧的 "Halcon" 命名空间引用）

6.3 确认输出目录中不再有 `Halcon.dll`（应为 `HalconWrapper.dll`），也不再有 `VM.Halcon.dll`

---

## 风险评估

| 风险 | 等级 | 缓解措施 |
|------|------|---------|
| 命名空间替换遗漏导致编译失败 | 中 | 全局搜索验证，编译检查 |
| 运行时 DLL 加载路径变化 | 低 | halcondotnet.dll 引用路径不变，仅项目自身 DLL 更名 |
| MainApp 移除 VM.Halcon 后运行异常 | 低 | MainApp 中无任何代码使用 VM.Halcon 命名空间，引用可能是遗留的 |
| Git 历史中断（目录重命名） | 低 | Git 会自动检测重命名 |

## 影响范围

- **HalconWrapper/** (原 Halcon/)：32 个 .cs 文件，1 个 .csproj 文件
- **GZQL_MACHINE.sln**：2 处修改（路径更新 + VM.Halcon 移除）
- **Module/Module.csproj**：1 处 ProjectReference 路径更新
- **Core/Core.csproj**：1 处 ProjectReference 路径更新
- **MainApp/MainApp.csproj**：1 处 ProjectReference 移除
- **Module/Controls/HalconCanvasControl.xaml.cs**：1 处 using 更新
- **VM.Halcon/**：整个目录删除
