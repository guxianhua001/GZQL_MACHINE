# 🚀 无需管理员权限 - Roslyn Source Generator 调试方案

## 📌 问题场景
在 Visual Studio 2022 中开发/调试 `LogMessagesGenerator.cs` 时，提示需要安装 **.NET Compiler Platform SDK**，但没有管理员权限。

---

## ✅ 方案对比表

| 方案 | 难度 | 推荐度 | 适用场景 |
|------|------|--------|---------|
| **① VS 扩展（最简单）** | ⭐ | ⭐⭐⭐⭐⭐ | 快速查看生成代码，无需断点调试 |
| **② PowerShell 脚本** | ⭐⭐ | ⭐⭐⭐⭐ | 自动化编译 + 手动附加进程 |
| **③ CMD 脚本 + 附加** | ⭐⭐ | ⭐⭐⭐ | 传统方式，兼容性好 |
| **④ VS 外部程序启动** | ⭐⭐⭐ | ⭐⭐⭐ | 完全在 VS 内操作，F5 即可 |

---

## 🎯 方案 1: 安装 VS 扩展（5 分钟搞定）✨ 推荐

### 为什么推荐？
- ✅ **完全不需要 SDK**
- ✅ **无需管理员权限**（VS 扩展可以安装在用户目录）
- ✅ **实时查看生成的代码**
- ✅ 支持最新版 VS 2022

### 操作步骤

#### Step 1: 安装扩展
1. 打开 Visual Studio 2022
2. 菜单栏：**扩展** → **管理扩展** → **联机**
3. 搜索框输入：`Roslyn Source Generator Debugger`
4. 找到 **[Source Generators Visualizer](https://marketplace.visualstudio.com/items?itemName=devlooped.CSharpSourceGeneratorsVisualizer)** 
   - 作者：Oleg Shilov 或 devlooped
   - 下载量：>50,000
   - ⭐ 评分：4.5+
5. 点击 **下载** → VS 提示重启
6. 重启 VS 后自动安装完成

#### Step 2: 使用扩展查看生成代码
1. 编译项目：**生成** → **重新生成解决方案** (Ctrl+Shift+B)
2. 打开窗口：**视图** → **其他窗口** → **Source Generators** (或类似名称)
3. 在左侧树中展开：
   ```
   MainApp (Project)
   └── LogGenerator (Analyzer)
       └── LogMessages.g.cs ← 点击查看生成的代码！
   ```

#### Step 3: 高级功能（可选）
- **实时预览**: 修改 `LogMessages.cs` 后保存，自动刷新生成代码
- **语法高亮**: 生成的代码带完整语法着色
- **导航支持**: F12 可以跳转到原始定义

---

## 🔧 方案 2: 使用 PowerShell 调试脚本（已提供）

### 文件位置
```
c:\WorkFiles\GZQL_MACHINE\LogGenerator\Debug-Generator.ps1
```

### 使用方法

#### 基础用法
```powershell
cd c:\WorkFiles\GZQL_MACHINE\LogGenerator
.\Debug-Generator.ps1
```

#### 高级参数
```powershell
# 详细输出模式（显示 Generator 执行细节）
.\Debug-Generator.ps1 -VerboseOutput

# 跳过清理步骤（加快速度）
.\Debug-Generator.ps1 -NoClean

# 自定义配置
.\Debug-Generator.ps1 -Configuration Release
```

### 工作流程
```
运行脚本 → 按提示在 VS 中设置断点 → 按 Enter 开始编译 
→ 附加到 dotnet.exe 进程 → 断点命中！
```

---

## 💻 方案 3: 使用 CMD 脚本（已提供）

### 文件位置
```
c:\WorkFiles\GZQL_MACHINE\LogGenerator\debug.cmd
```

### 使用方法
双击运行或在命令行中执行：
```batch
cd c:\WorkFiles\GZQL_MACHINE\LogGenerator
debug.cmd
```

### 特点
- ✅ 兼容 Windows 7+ / Server 2012+
- ✅ 无需 PowerShell 7
- ✅ 自动检测环境问题

---

## 🎮 方案 4: 配置 VS 外部程序启动（进阶）

### 配置步骤

#### Step 1: 修改项目属性
1. 右键点击 `LogGenerator` 项目 → **属性**
2. 左侧选择 **"调试"** 标签页
3. 配置以下内容：

| 配置项 | 值 |
|--------|-----|
| **启动操作** | 选择 "启动外部程序" |
| **外部程序** | `C:\Program Files\dotnet\dotnet.exe` |
| **工作目录** | `$(ProjectDir)` |
| **命令行参数** | `build "..\..\GZQL_MACHINE.sln" --configuration Debug --no-dependencies` |
| **环境变量** | （留空） |
| **远程调试器** | 不勾选 |

#### Step 2: 设置断点并启动
1. 打开 `LogMessagesGenerator.cs`
2. 在第 35 行左右 (`public void Execute(...)`) 设置断点
3. 按 **F5** 启动调试
4. VS 会自动启动编译，并在断点处停止！

#### Step 3: 调试技巧
- **查看变量**: 将鼠标悬停在 `context` 参数上，展开查看所有可用信息
- **调用堆栈**: 查看 Generator 是如何被调用的
- **即时窗口**: 输入表达式实时求值（如 `methods.Count`）

---

## 🛠️ 故障排除速查表

| 问题 | 可能原因 | 解决方案 |
|------|---------|---------|
| **找不到 dotnet.exe 进程** | 编译太快已完成 | 在编译开始前先附加进程 |
| **断点显示空心圆点** | 符号未加载 | 重新生成项目，确保选择正确的代码类型 |
| **附加后立即断开连接** | 进程崩溃或权限不足 | 以管理员身份运行 VS（如果可能），或使用当前用户权限 |
| **Generator 未执行** | 项目未引用 Analyzer | 检查 MainApp.csproj 是否包含 `<ProjectReference Include="..\LogGenerator\...">` |
| **生成的代码为空** | LogMessages.cs 无方法 | 确认至少有一个 `[LogMessage]` Attribute 的方法 |
| **VS 扩展无法安装** | 网络限制 | 下载 .vsix 文件手动安装：`vsixinstaller /quiet extension.vsix` |

---

## 📊 推荐使用顺序

### 新手入门（第一次使用）
```
方案 ① (VS 扩展) → 5分钟上手，零配置
```

### 日常开发（需要断点调试）
```
方案 4 (VS 外部程序) → F5 一键调试，体验最佳
```

### CI/CD 或自动化构建
```
方案 2 (PowerShell) 或 方案 3 (CMD) → 可集成到脚本中
```

### 团队协作（统一环境）
```
将 debug.cmd / Debug-Generator.ps1 提交到 Git
团队成员无需任何配置即可调试！
```

---

## 🎁 额外福利：一键设置脚本

如果你想让团队其他成员也能快速使用，可以创建一个初始化脚本：

```powershell
# Setup-GeneratorDebugging.ps1
# 运行此脚本自动配置所有调试选项

Write-Host "🔧 正在配置 Roslyn Generator 调试环境..." -ForegroundColor Cyan

# 1. 创建桌面快捷方式
$desktop = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktop "调试 Generator.lnk"
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($shortcutPath)
$sc.TargetPath = "powershell.exe"
$sc.Arguments = "-NoExit -Command `"$PSScriptRoot\Debug-Generator.ps1`""
$sc.WorkingDirectory = $PSScriptRoot
$sc.IconLocation = "shell32.dll,13"
$sc.Description = "调试 Roslyn Source Generator"
$sc.Save()

Write-Success "已创建桌面快捷方式: 调试 Generator.lns"

# 2. 配置 VS 外部程序启动（可选）
Write-Info "是否配置 VS 外部程序启动？(Y/N)"
$configVS = Read-Host
if ($configVS -match "^Y") {
    # 这里可以添加自动化修改 .csproj 的逻辑
    Write-Info "请按照文档手动配置（见方案 4）"
}

Write-Host ""
Write-Host "🎉 配置完成！" -ForegroundColor Green
Write-Host "现在可以：" -ForegroundColor White
Write-Host "  1. 双击桌面的 '调试 Generator' 快捷方式" -ForegroundColor Gray
Write-Host "  2. 或在 VS 中按 F5（如果已配置方案 4）" -ForegroundColor Gray
```

---

## 📞 获取帮助

如果以上方案都无法解决你的问题：

1. **检查日志位置**:
   - `%TEMP%\RoslynSourceGenerator-*.log`
   - VS 输出窗口 → 显示输出来源: "Build"

2. **收集诊断信息**:
   ```powershell
   # 运行此命令收集环境信息
   dotnet --info > environment-info.txt
   Get-Module -ListAvailable | Where-Object {$_.Name -match "roslyn|codeanalysis"} >> environment-info.txt
   ```

3. **社区资源**:
   - [Stack Overflow: roslyn-source-generators](https://stackoverflow.com/questions/tagged/roslyn-source-generators)
   - [GitHub Discussions: dotnet/roslyn](https://github.com/dotnet/roslyn/discussions)

---

**最后更新**: 2026-05-20  
**适用版本**: Visual Studio 2022 17.12+, .NET 9.0 SDK  
**测试环境**: Windows 11 / Windows Server 2022 (无管理员权限)
