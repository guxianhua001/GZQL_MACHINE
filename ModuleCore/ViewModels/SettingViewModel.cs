using Framework.Mvvm;
using Core.Abstraction;
using Core.Services;
using ModuleCore.Common.Authority;
using ModuleCore.Models;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace ModuleCore.ViewModels
{
    /// <summary>
    /// 设置页面 ViewModel：管理设备配置、轴参数、安全区域、主题等设置
    /// </summary>
    public class SettingViewModel : RegionViewModelBase
    {
        private DelegateCommand _Load;
        private DelegateCommand _Save;
        private DelegateCommand _toggleThemeCommand;
        private readonly IThemeService _themeService;
        private DataTable dt;
        private List<string> ShowList = new();
        IRegionManager _regionManager;
        public static string _configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Config",
            "ViewConfig.json");

        /// <summary>
        /// 构造函数：注入容器、区域管理器、主题服务
        /// </summary>
        public SettingViewModel(IContainerExtension container, IRegionManager regionManager, IThemeService themeService) : base(regionManager)
        {
            _regionManager = regionManager;
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            Navigate = container.Resolve<NavigateModel>();
            Model = container.Resolve<LoginModel>();

            // 订阅全局主题变化，同步 UI 状态
            _themeService.ThemeChanged += OnThemeChanged;
        }

        /// <summary>当前是否为暗色主题</summary>
        public bool IsDarkTheme => _themeService.IsDarkTheme;

        /// <summary>切换主题命令</summary>
        public DelegateCommand ToggleThemeCommand =>
            _toggleThemeCommand ??= new DelegateCommand(ExecuteToggleTheme);

        public DelegateCommand Load =>
            _Load ??= new DelegateCommand(ExecuteLoad);

        public LoginModel Model { get; set; }
        public NavigateModel Navigate { get; set; }
        public DelegateCommand Save =>
             _Save ??= new DelegateCommand(ExecuteSave);

        /// <summary>执行主题切换</summary>
        private void ExecuteToggleTheme()
        {
            _themeService.ToggleTheme();
        }

        /// <summary>主题变化回调：通知 UI 属性更新</summary>
        private void OnThemeChanged(bool isDark)
        {
            RaisePropertyChanged(nameof(IsDarkTheme));
        }

        private void ExecuteLoad()
        {
            dt = JsonService.DataTableFromFile(_configPath);//"./Config/ViewConfig.json"
            if (dt == null)
            {
                return;
            }
            else
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    var viewname = dt.Rows[i]["ViewName"].ToString();
                    if (!ShowList.Contains(viewname))
                    {
                        ShowList.Add(viewname);
                    }
                }

                foreach (var item in Navigate.NavigateList)
                {
                    if (ShowList.Contains(item.ViewName))
                    {
                        item.Display = true;
                    }
                    else
                    {
                        item.Display = false;
                    }
                }
                ShowNavigateMenu(Model.LoginUser.Authority);
            }
        }

        private void ExecuteSave()
        {
            ShowNavigateMenu(Model.LoginUser.Authority);

            dt = new DataTable();

            dt.Columns.Add("ViewName", Type.GetType("System.String"));

            foreach (var item in Navigate.NavigateShowList)
            {


                DataRow dr = dt.NewRow();
                dt.Rows.Add(dr);

                dr["ViewName"] = item.ViewName;

            }
            JsonService.DataTableToFile(_configPath, dt);//"./Config/ViewConfig.json"
        }
        private void ShowNavigateMenu(Authority authority)
        {
            Navigate.NavigateShowList.Clear();

            foreach (var item in Navigate.NavigateList)
            {
                if (item.UserLevel <= (int)authority && item.Display)
                    Navigate.NavigateShowList.Add(item);
            }
        }
    }
}