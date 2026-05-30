using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class InspectionViewModel : BindableBase
    {
        private readonly IDialogService _dialogService;

        private ObservableCollection<InspectionResult> _inspectionResults;
        private bool _recordSaved = true;
        private ObservableCollection<string> _siteList;
        private string _selectedSite;

        public ObservableCollection<InspectionResult> InspectionResults
        {
            get => _inspectionResults;
            set => SetProperty(ref _inspectionResults, value);
        }

        public bool RecordSaved
        {
            get => _recordSaved;
            set => SetProperty(ref _recordSaved, value);
        }

        public ObservableCollection<string> SiteList
        {
            get => _siteList;
            set => SetProperty(ref _siteList, value);
        }

        public string SelectedSite
        {
            get => _selectedSite;
            set => SetProperty(ref _selectedSite, value);
        }

        public ICommand MoveCameraCommand { get; }
        public ICommand MeasureWidthCommand { get; }
        public ICommand EditSitePositionCommand { get; }
        public ICommand ExportCSVCommand { get; }

        public InspectionViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;

            MoveCameraCommand = new DelegateCommand(OnMoveCamera);
            MeasureWidthCommand = new DelegateCommand(OnMeasureWidth);
            EditSitePositionCommand = new DelegateCommand(OnEditSitePosition);
            ExportCSVCommand = new DelegateCommand(OnExportCSV);

            // 初始化 Site 列表
            SiteList = new ObservableCollection<string>
            {
                "PILLAR_001",
                "TAB_001",
                "SLOT_001",
                "PIN_001"
            };
            SelectedSite = "PILLAR_001";

            // 示例数据
            InspectionResults = new ObservableCollection<InspectionResult>
            {
                new InspectionResult { Site = "DISP_001", Type = "Dot", Width = 1.1, Result = true },
                new InspectionResult { Site = "DISP_005", Type = "2D Line", Width = 0.9, Result = true },
                new InspectionResult { Site = "DISP_011", Type = "3D Line", Width = 1.3, Result = false }
            };
        }

        private void OnMoveCamera()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Moving camera to dot/line position." } }, null);
        }

        private void OnMeasureWidth()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Measuring width and continuity..." } }, null);
            // 模拟测量更新表格数据
            // 实际可调用服务刷新数据
        }

        private void OnEditSitePosition()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", $"Editing position for site: {SelectedSite}" } }, null);
        }

        private void OnExportCSV()
        {
            // 模拟导出 CSV
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Exporting inspection data to CSV..." } }, null);
            // 实际可实现文件保存逻辑
        }
    }

    public class InspectionResult : BindableBase
    {
        private string _site;
        private string _type;
        private double _width;
        private bool _result;

        public string Site
        {
            get => _site;
            set => SetProperty(ref _site, value);
        }

        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public bool Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }
    }
}