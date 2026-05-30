#Requires -Version 7.0
<#
.SYNOPSIS
    Roslyn Source Generator 调试助手（无需管理员权限）
.DESCRIPTION
    自动编译解决方案并提示附加 Visual Studio 调试器
.EXAMPLE
    .\Debug-Generator.ps1
.NOTES
    作者: GZQL_MACHINE Team
    日期: 2026-05-20
#>

[CmdletBinding()]
param(
    [string]$SolutionPath = (Join-Path $PSScriptRoot "..\GZQL_MACHINE.sln"),
    [string]$Configuration = "Debug",
    [switch]$NoClean,
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"

# ═══════════════════════════════════════════════════════════════
#   UI 辅助函数
# ═══════════════════════════════════════════════════════════════

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host "═" * 70 -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host "═" * 70 -ForegroundColor Cyan
    Write-Host ""
}

function Write-Success {
    param([string]$Text)
    Write-Host "  ✅ $Text" -ForegroundColor Green
}

function Write-Error2 {
    param([string]$Text)
    Write-Host "  ❌ $Text" -ForegroundColor Red
}

function Write-Warning2 {
    param([string]$Text)
    Write-Host "  ⚠️  $Text" -ForegroundColor Yellow
}

function Write-Info {
    param([string]$Text)
    Write-Host "  ℹ️  $Text" -ForegroundColor Gray
}

# ═══════════════════════════════════════════════════════════════
#   主程序
# ═══════════════════════════════════════════════════════════════

try {
    # 检查前置条件
    Write-Header "环境检查"

    # 检查 dotnet CLI
    $dotnetVersion = dotnet --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error2 "未找到 .NET SDK！请先安装 .NET 9.0 SDK"
        exit 1
    }
    Write-Success ".NET SDK 版本: $dotnetVersion"

    # 检查解决方案文件
    if (-not (Test-Path $SolutionPath)) {
        Write-Error2 "找不到解决方案文件: $SolutionPath"
        exit 1
    }
    Write-Success "解决方案文件: $(Resolve-Path $SolutionPath)"

    # 检查 Generator 文件
    $generatorFile = Join-Path $PSScriptRoot "LogMessagesGenerator.cs"
    if (-not (Test-Path $generatorFile)) {
        Write-Error2 "找不到 Generator 文件: $generatorFile"
        exit 1
    }
    Write-Success "Generator 文件: LogMessagesGenerator.cs"

    # Step 1: 清理
    if (-not $NoClean) {
        Write-Header "Step 1/3: 清理旧的构建输出"
        
        $cleanResult = dotnet clean $SolutionPath --configuration $Configuration --verbosity quiet 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "清理完成"
        } else {
            Write-Warning2 "清理失败（可忽略）: $cleanResult"
        }
    }

    # Step 2: 编译前准备
    Write-Header "Step 2/3: 准备调试环境"
    
    Write-Host ""
    Write-Host "  🎯 即将开始编译，请按以下步骤操作：" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "  1️⃣  打开 Visual Studio 2022" -ForegroundColor White
    Write-Host "  2️⃣  打开文件: LogMessagesGenerator.cs" -ForegroundColor White
    Write-Host "  3️⃣  在 Execute() 方法第 1 行设置断点" -ForegroundColor White
    Write-Host "  4️⃣  按 Ctrl+Alt+P 打开'附加到进程'对话框" -ForegroundColor White
    Write-Host "  5️⃣  查找并选择 dotnet.exe 进程" -ForegroundColor White
    Write-Host "  6️⃣  确保选择代码类型: 'Managed (.NET Core, .NET 5+)'" -ForegroundColor White
    Write-Host "  7️⃣  点击'附加'" -ForegroundColor White
    Write-Host ""
    Write-Host "  💡 提示: 断点将在编译开始后 2-5 秒内被命中" -ForegroundColor DarkGray
    Write-Host ""

    $null = Read-Host "  按 Enter 键开始编译（请先完成上述步骤）"

    # Step 3: 开始编译
    Write-Header "Step 3/3: 正在编译（Generator 执行中...）"
    Write-Host ""
    Write-Info "编译参数: Configuration=$Configuration, Verbose=$(if($VerboseOutput){'diagnostic'}else{'normal'})"
    Write-Host ""

    $verbosity = if ($VerboseOutput) { "diagnostic" } else { "normal" }
    
    # 启动编译（使用 Start-Process 以便可以监控进程）
    $buildProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @(
            "build",
            "`"$SolutionPath`"",
            "--configuration", $Configuration,
            "--verbosity", $verbosity,
            '/p:SuppressBuildInParallelization=true'
        ) `
        -PassThru `
        -NoNewWindow

    # 等待进程完成（最多等待 5 分钟）
    $timeout = 300  # 5 分钟
    $elapsed = 0
    
    while (-not $buildProcess.HasExited -and $elapsed -lt $timeout) {
        Start-Sleep -Milliseconds 500
        $elapsed += 0.5
        
        # 显示进度点（避免刷屏）
        if ($elapsed % 10 -eq 0) {
            Write-Host "." -NoNewline -ForegroundColor DarkGray
        }
    }

    Write-Host ""  # 换行

    if ($buildProcess.HasExited) {
        if ($buildProcess.ExitCode -eq 0) {
            Write-Header "✅ 编译成功！"
            
            Write-Host ""
            Write-Host "  🎉 Roslyn Source Generator 已成功执行！" -ForegroundColor Green
            Write-Host ""
            Write-Host "  📁 生成的文件位置:" -ForegroundColor White
            Write-Host "     MainApp\obj\Debug\net9.0-windows7.0\LogMessages.g.cs" -ForegroundColor Gray
            Write-Host ""
            Write-Host "  🔍 验证方法:" -ForegroundColor White
            Write-Host "     1. 在 VS 解决方案资源管理器中，展开 MainApp → Dependencies → Analyzers" -ForegroundColor Gray
            Write-Host "     2. 展开 LogGenerator → 查看 LogMessages.g.cs" -ForegroundColor Gray
            Write-Host "     3. 双击打开查看生成的代码" -ForegroundColor Gray
            Write-Host ""
            
            # 尝试打开生成的文件（可选）
            $generatedFile = Join-Path $PSScriptRoot "..\MainApp\obj\Debug\net9.0-windows7.0\LogMessages.g.cs"
            if (Test-Path $generatedFile) {
                Write-Info "是否打开生成的文件？(Y/N)"
                $openFile = Read-Host
                if ($openFile -match "^Y") {
                    Invoke-Item $generatedFile
                }
            }
        } else {
            Write-Header "❌ 编译失败"
            Write-Host ""
            Write-Error2 "退出代码: $($buildProcess.ExitCode)"
            Write-Host ""
            Write-Host "  常见问题排查:" -ForegroundColor Yellow
            Write-Host "  1. NuGet 包未还原 → 运行: dotnet restore `"$SolutionPath`"" -ForegroundColor Gray
            Write-Host "  2. Generator 语法错误 → 检查 LogMessagesGenerator.cs 第 1-50 行" -ForegroundColor Gray
            Write-Host "  3. Attribute 缺失 → 确认 LogMessageAttribute.cs 已添加到 Core 项目" -ForegroundColor Gray
            Write-Host ""
        }
    } else {
        Write-Warning2 "编译超时（${timeout}秒），进程可能卡住"
        Write-Info "尝试终止进程..."
        $buildProcess.Kill()
    }

} catch {
    Write-Header "发生未预期的错误"
    Write-Error2 $_.Exception.Message
    Write-Host ""
    Write-Host "堆栈跟踪:" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkRed
    exit 1
}

finally {
    Write-Host ""
    Write-Info "按任意键退出..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
