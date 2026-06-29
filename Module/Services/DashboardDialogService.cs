using Core.Abstraction;
using Core.Utilities;
using MaterialDesignThemes.Wpf;
using Module.ViewModels;
using Module.Views;
using Prism.Events;
using Prism.Ioc;
using StationTasks.Events;
using System;
using System.Threading.Tasks;

namespace Module.Services
{
    /// <summary>
    /// 看板弹窗服务：订阅 ShowDashboardEvent，通过 DialogHost 显示看板界面，
    /// 用户操作后发布 DashboardConfirmedEvent 通知执行器继续流程
    /// </summary>
    public class DashboardDialogService
    {
        private readonly IEventAggregator _ea;
        private readonly IContainerProvider _containerProvider;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        private const string DialogIdentifier = "MainDialogHost";

        /// <summary>获取多语言格式化字符串</summary>
        private string L(string key, string fallback, params object[] args)
        {
            var format = _localization?.GetResourceOrDefault(key, fallback) ?? fallback;
            return args.Length > 0 ? string.Format(format, args) : format;
        }

        public DashboardDialogService(IEventAggregator ea, IContainerProvider containerProvider, ILoggerService logger, ILocalizationService localization)
        {
            _ea = ea;
            _containerProvider = containerProvider;
            _logger = logger;
            _localization = localization;

            // 订阅看板显示事件（必须在 UI 线程执行 DialogHost.Show）
            _ea.GetEvent<ShowDashboardEvent>().Subscribe(OnShowDashboard, Prism.Events.ThreadOption.UIThread);
        }

        /// <summary>
        /// 处理看板显示请求（执行模式和编辑模式都使用此方法）
        /// </summary>
        private async void OnShowDashboard(ShowDashboardPayload payload)
        {
            try
            {
                _logger.Info(L("DashDlg_Log_ReceivedRequest", "[DashboardDialog] 收到看板请求, IsExecutionMode={0}", payload.IsExecutionMode));

                // 创建 ViewModel 和 View，在 UI 线程直接注入载荷（IF/ELSE 子步骤从后台线程发布事件）
                var vm = _containerProvider.Resolve<DataDashboardViewModel>();
                vm.ApplyPayload(payload);
                var view = new DataDashboardView { DataContext = vm };

                // 通过 DialogHost 显示弹窗（等待用户操作）
                var result = await DialogHost.Show(view, DialogIdentifier);

                _logger.Info(L("DashDlg_Log_DialogClosed", "[DashboardDialog] 弹窗已关闭, result={0}", result));
            }
            catch (Exception ex)
            {
                _logger.Error(L("DashDlg_Log_DialogException", "[DashboardDialog] 看板弹窗异常: {0}", ex.Message));
            }
        }
    }
}
