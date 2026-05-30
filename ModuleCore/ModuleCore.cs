using Core.Abstraction;
using ModuleCore.Models;
using ModuleCore.Views;
using Prism.Ioc;
using Prism.Modularity;
using System.Collections.Concurrent;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using ModuleCore.ViewModels;
using LiveCharts.Wpf;
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
            containerRegistry.RegisterDialog<LogView, ViewModels.LogViewViewModel>();
            containerRegistry.RegisterDialog<ConfirmationDialog, ViewModels.ConfirmationDialogViewModel>();
            containerRegistry.RegisterDialog<ErrorDialog, ViewModels.ErrorDialogViewModel>();
            //注入导航
            containerRegistry.RegisterForNavigation<Setting>();

        }
    }

}
