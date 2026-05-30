@echo off
chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion

echo ══════════════════════════════════════════════════════════
echo    Roslyn Source Generator - 无管理员权限调试模式
echo ══════════════════════════════════════════════════════════
echo.

:: 检查 .NET SDK 是否安装
where dotnet >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [错误] 未找到 dotnet 命令，请确保已安装 .NET 9.0 SDK
    pause
    exit /b 1
)

:: 显示当前环境信息
echo [信息] 当前目录: %CD%
echo [信息] .NET 版本:
dotnet --version
echo.

set "SOLUTION_PATH=%~dp0..\GZQL_MACHINE.sln"
set "GENERATOR_PATH=%~dp0LogMessagesGenerator.cs"

:: 检查解决方案文件是否存在
if not exist "%SOLUTION_PATH%" (
    echo [错误] 找不到解决方案文件: %SOLUTION_PATH%
    pause
    exit /b 1
)

:: 检查 Generator 文件是否存在
if not exist "%GENERATOR_PATH%" (
    echo [错误] 找不到 Generator 文件: %GENERATOR_PATH%
    pause
    exit /b 1
)

echo ══════════════════════════════════════════════════════════
echo    Step 1/3: 清理旧的构建输出...
echo ══════════════════════════════════════════════════════════
dotnet clean "%SOLUTION_PATH%" --configuration Debug --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo [警告] 清理失败，继续执行...
)
echo [✓] 清理完成
echo.

echo ══════════════════════════════════════════════════════════
echo    Step 2/3: 开始编译（Generator 将在此过程中执行）...
echo ══════════════════════════════════════════════════════════
echo.
echo ⚠️  重要提示：
echo    请在 Visual Studio 中执行以下操作来调试 Generator:
echo.
echo    1. 打开 LogMessagesGenerator.cs 文件
echo    2. 在 Execute() 方法中设置断点
echo    3. 按 Ctrl+Alt+P (调试 → 附加到进程)
echo    4. 选择 dotnet.exe 或 MSBuild.exe 进程
echo    5. 点击"附加"按钮
echo    6. 断点将在几秒内被命中！
echo.
echo 正在启动编译...
echo ----------------------------------------

:: 使用 diagnostic 级别输出（可以看到 Generator 执行详情）
dotnet build "%SOLUTION_PATH%" ^
    --configuration Debug ^
    --verbosity diagnostic ^
    /p:SuppressBuildInParallelization=true

set BUILD_RESULT=%ERRORLEVEL%

echo ----------------------------------------
echo.

if %BUILD_RESULT% EQU 0 (
    echo ══════════════════════════════════════════════════════════
    echo    ✅ 编译成功！
    echo ══════════════════════════════════════════════════════════
    echo.
    echo 🎉 Generator 已成功执行！
    echo.
    echo 如果断点未被命中，请尝试：
    echo   1. 在"附加进程"对话框中选择"Managed (.NET Core)"代码类型
    echo   2. 确保在编译开始前就已经附加进程
    echo   3. 检查断点是否为实心红点（非空心）
) else (
    echo ══════════════════════════════════════════════════════════
    echo    ❌ 编译失败 (错误代码: %BUILD_RESULT%)
    echo ══════════════════════════════════════════════════════════
    echo.
    echo 可能的原因：
    echo   1. NuGet 包未还原 → 运行: dotnet restore
    echo   2. 项目文件路径错误 → 检查解决方案路径
    echo   3. Generator 代码有语法错误 → 查看 LogMessagesGenerator.cs
)

echo.
pause
endlocal
