using Interfaces;
using ModuleCore.Models;
using ModuleCore.Views;
using Prism.Ioc;
using Prism.Modularity;
using SmarterMotion;
using Stations;
using System.Collections.Concurrent;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using ModuleCore.ViewModels;
using HSMS;
using AxisConfiguration.ViewModels;
using AxisConfiguration.Services;
using LiveCharts.Wpf;
using ModuleCore.Services;
using Interfaces.SharedInterfaces;
using ModuleCore.Configs;
using Framework.Mvvm;

namespace ModuleCore
{
    public class CoreModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            if (!Directory.Exists("./Config/")) Directory.CreateDirectory("./Config/");
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            //注册导航菜单
            _ = containerRegistry.RegisterSingleton<NavigateModel>();

            //注册权限系统
            _ = containerRegistry.RegisterSingleton<LoginModel>();
            //注册窗体
            containerRegistry.RegisterDialog<AlertDialog, ViewModels.AlertDialogViewModel>();
            containerRegistry.RegisterDialog<RegistView, ViewModels.RegistViewModel>();
            containerRegistry.RegisterDialog<LoginView, ViewModels.LoginViewModel>();
            containerRegistry.RegisterDialog<UserManage, ViewModels.UserManageViewModel>();
            containerRegistry.RegisterDialog<PasswordChange, ViewModels.PasswordChangeViewModel>();
            containerRegistry.RegisterDialog<ModuleCore.Views.LogViewer, ViewModels.LogViewerViewModel>();
            containerRegistry.RegisterDialog<ConfirmationDialog, ViewModels.ConfirmationDialogViewModel>();
            containerRegistry.RegisterDialog<Views.NotificationDialog, ViewModels.NotificationDialogViewModel>(name: "NotificationDialog");
            containerRegistry.RegisterDialog<Views.ErrorDialog, ViewModels.ErrorDialogViewModel>();
            //注入导航
            containerRegistry.RegisterForNavigation<Setting>();
            containerRegistry.RegisterForNavigation<NeedleView>();
            containerRegistry.RegisterForNavigation<Views.AlarmReportingView, ViewModels.AlarmReportingViewModel>();
            containerRegistry.RegisterSingleton<NeedleViewModel>();
            containerRegistry.RegisterSingleton<EquipmentStatus>();
            containerRegistry.RegisterForNavigation<TaskCardView, TaskCardViewModel>();
            containerRegistry.RegisterForNavigation<TaskMonitorView, TaskMonitorViewModel>();
            containerRegistry.RegisterForNavigation<AxisSettingView, AxisSettingViewModel>();
            //注册服务
            containerRegistry.RegisterSingleton<INeedleService, NeedleViewModel>();


            //注册龙门同步


            // 1. 配置并注册两个龙门系统
            var system1Config = new System1Config { };
            var system2Config = new System2Config { };

            //var system1Service = new GantrySyncService(1,system1Config);
            //var system2Service = new GantrySyncService(2,system2Config);

            // 注册系统集合（供 CurrentSystemService 使用）
            //var systems = new[] { system1Service, system2Service };
            //containerRegistry.RegisterInstance<IEnumerable<IGantrySyncService>>(systems);

            // 2. 注册集中系统管理服务
            containerRegistry.RegisterSingleton<ICurrentSystemService, CurrentSystemService>();

            // 3. 注册控制面板服务
            containerRegistry.Register<IControlPanelService, ControlPanelService>();

            // 4. 注册视图
            containerRegistry.RegisterForNavigation<StatusDashboardView, StatusDashboardViewModel>();

        }
    }

}
