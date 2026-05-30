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
        void AddTask(bool isDefault = false);
        void DeleteTask();
        void AutoGenerate();

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

        /// <summary> 单独执行指定步骤（用于步骤编辑器中的调试运行） </summary>
        Task RunSingleStepAsync(ProcessStep step);

        event EventHandler WorkOrderDataRefreshed;
    }
}