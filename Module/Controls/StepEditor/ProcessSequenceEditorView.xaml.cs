using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Module.ViewModels;
using StationTasks.Models;

namespace Module.Views
{
    public partial class ProcessSequenceEditorView : UserControl
    {
        private static readonly string LogFile = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Debug", "StepEditorDoubleClick.log");

        public ProcessSequenceEditorView()
        {
            InitializeComponent();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LogFile));
        }

        /// <summary>
        /// 双击步骤行时打开详细编辑弹窗
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            LogDebug($"[DoubleClick] 事件触发 sender={sender?.GetType().Name}");

            if (DataContext is ProcessSequenceEditorViewModel vm)
            {
                var selectedStep = vm.SelectedStep;
                LogDebug($"[DoubleClick] ViewModel已获取, SelectedStep={(selectedStep != null ? $"Seq={selectedStep.Seq}, Step={selectedStep.Step}, CompFeature={selectedStep.CompFeature}" : "NULL")}");

                if (selectedStep == null)
                {
                    LogDebug("[DoubleClick] ❌ SelectedStep为null，无法打开弹窗");
                    return;
                }

                LogDebug($"[DoubleClick] 调用 OpenStepDetail()...");
                vm.OpenStepDetail();
                LogDebug($"[DoubleClick] OpenStepDetail() 调用完成");
            }
            else
            {
                LogDebug($"[DoubleClick] ❌ DataContext不是 ProcessSequenceEditorViewModel，实际类型={DataContext?.GetType().Name ?? "null"}");
            }
        }

        /// <summary>
        /// 写入调试日志到文件和 Debug 输出
        /// </summary>
        private void LogDebug(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            Debug.WriteLine(line);
            try
            {
                System.IO.File.AppendAllText(LogFile, line + Environment.NewLine);
            }
            catch { /* 忽略写入失败 */ }
        }
    }
}
