using Core.Models;
using Module.Models;
using StationTasks.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Module.Services
{
    public interface IProcessSequenceService:  INotifyPropertyChanged 
    {
        // 任务与步骤管理
        ObservableCollection<TaskItem> Tasks { get; }
        TaskItem CurrentTask { get; set; }
        ProcessStep SelectedStep { get; set; }
        int CurrentStepIndex { get; set; }

        void AddStep(ProcessStep step);
        void DeleteStep();
        void MoveStepUp();
        void MoveStepDown();

        /// <summary> 将步骤移动到指定方法的指定位置（用于拖拽排序，方法顶层） </summary>
        void MoveStepTo(ProcessStep step, ProcessMethod targetMethod, int targetIndex);

        /// <summary> 拖拽排序：拖到目标步骤位置（支持 IF 分支内子步骤） </summary>
        void MoveStepTo(ProcessStep draggedStep, ProcessStep targetStep);

        /// <summary> 判断两步骤是否可拖拽排序（同方法顶层或同 IF 分支组） </summary>
        bool CanMoveStepTo(ProcessStep draggedStep, ProcessStep targetStep);

        /// <summary> 将任务移动到指定位置（用于拖拽排序） </summary>
        /// <param name="task">要移动的任务</param>
        /// <param name="targetIndex">目标索引（-1 表示追加到末尾）</param>
        void MoveTaskTo(TaskItem task, int targetIndex);

        void AddTask(bool isDefault = false);
        void DeleteTask();
        void AutoGenerate();

        /// <summary> 当前选中的方法节点 </summary>
        ProcessMethod SelectedMethod { get; set; }

        /// <summary> 当前选中的树节点（TaskItem / ProcessMethod / ProcessStep 之一） </summary>
        object SelectedNode { get; set; }

        /// <summary> 在当前任务下新建方法 </summary>
        void AddMethod();
        /// <summary> 删除当前选中的方法 </summary>
        void DeleteMethod();
        /// <summary> 重命名当前选中的方法 </summary>
        void RenameMethod(string newName);
        /// <summary> 复制当前选中节点到剪贴板 </summary>
        void CopyNode();
        /// <summary> 粘贴剪贴板节点到当前选中节点下 </summary>
        void PasteNode();
        /// <summary> 切换当前选中节点的启用/禁用状态 </summary>
        void ToggleNodeEnabled();
        /// <summary> 设置当前选中节点的注释 </summary>
        void EditNodeComment(string comment);
        /// <summary> 设置当前任务的运行模式 </summary>
        void SetTaskRunMode(TaskRunMode mode);

        ObservableCollection<ValidationItem> Validate();
        Task SaveSequenceToPathAsync(string filePath);   // 保存所有任务到指定路径
        Task SaveSequenceAsync(string stationId = null); // 自动保存到默认目录，文件名格式：{stationId}_{timestamp}.json
        Task LoadSequenceFromPathAsync(string filePath); // 加载所有任务

        /// <summary> 当前加载的序列文件路径，自动加载或手动加载后更新 </summary>
        string CurrentFilePath { get; set; }

        /// <summary> 最近使用的序列文件路径列表 </summary>
        ObservableCollection<string> RecentFiles { get; }

        /// <summary> 将文件路径记录到 MRU 列表并持久化 </summary>
        void RecordRecentFile(string filePath);

        Task LoadWorkOrderDataAsync();
        Task ReloadWorkOrderDataAsync();

        ObservableCollection<string> CameraOptions { get; }
        ObservableCollection<string> PurposeOptions { get; }
        ObservableCollection<string> ComponentFeatureOptions { get; }
        ObservableCollection<string> SiteFeatureOptions { get; }
        Models.Component SelectedComponent { get; set; }
        Site SelectedSite { get; set; }
        ObservableCollection<Models.Component> Components { get; }
        ObservableCollection<Site> Sites { get; }

        // 任务控制
        bool IsExecuting { get; }
        void StartTask();
        void StopTask();
        void PauseTask();
        void ResumeTask();

        // 方法级控制（控制单个方法独立执行）
        /// <summary> 是否有方法正在执行（与任务级执行互斥） </summary>
        bool IsMethodExecuting { get; }
        /// <summary> 当前正在执行的方法（null 表示无方法在执行） </summary>
        ProcessMethod ExecutingMethod { get; }
        /// <summary> 启动指定方法的独立执行（仅执行该方法的启用步骤） </summary>
        /// <param name="method">要执行的方法</param>
        void StartMethod(ProcessMethod method);
        /// <summary> 暂停当前正在执行的方法 </summary>
        void PauseMethod();
        /// <summary> 恢复当前被暂停的方法 </summary>
        void ResumeMethod();
        /// <summary> 停止当前正在执行的方法 </summary>
        void StopMethod();

        /// <summary> 是否启用单步模式（每步执行后等待用户确认再继续） </summary>
        bool IsSingleStepMode { get; set; }
        /// <summary> 单步模式下触发下一步执行 </summary>
        void StepNext();

        /// <summary> 单独执行指定步骤（用于步骤编辑器中的调试运行） </summary>
        Task RunSingleStepAsync(ProcessStep step);

        event EventHandler WorkOrderDataRefreshed;
    }
}