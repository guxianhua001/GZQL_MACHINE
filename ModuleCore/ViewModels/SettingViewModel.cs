using Framework.Mvvm;
using Core.Services;
using ModuleCore.Common.Authority;
using ModuleCore.Models;
using Prism.Commands;
using Prism.Ioc;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace ModuleCore.ViewModels
{
    public class SettingViewModel : RegionViewModelBase
    {
        private DelegateCommand _Load;
        private DelegateCommand _Save;
        private DataTable dt;
        private List<string> ShowList = new();
        IRegionManager _regionManager;
        public static string _configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Config",
            "ViewConfig.json");

        public SettingViewModel(IContainerExtension container, IRegionManager regionManager) : base(regionManager)
        {
            _regionManager = regionManager;
            Navigate = container.Resolve<NavigateModel>();
            Model = container.Resolve<LoginModel>();
        }

        public DelegateCommand Load =>
            _Load ??= new DelegateCommand(ExecuteLoad);

        public LoginModel Model { get; set; }
        public NavigateModel Navigate { get; set; }
        public DelegateCommand Save =>
             _Save ??= new DelegateCommand(ExecuteSave);

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