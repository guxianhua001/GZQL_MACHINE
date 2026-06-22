using Core.Abstraction;
using ModuleCore.Models;
using ModuleCore.Services;
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

            // 从配置加载主题并应用
            var themeService = containerProvider.Resolve<IThemeService>();
            themeService.LoadThemeFromSettings();
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            //注册导航菜单
            _ = containerRegistry.RegisterSingleton<NavigateModel>();

            //注册权限系统
            _ = containerRegistry.RegisterSingleton<LoginModel>();

            // 注册全局主题服务
            containerRegistry.RegisterSingleton<Core.Abstraction.IThemeService, ThemeService>();

            // 注册基础对话框服务（替代 DialogHost 用于 StepDetails）
            containerRegistry.RegisterSingleton<Core.Abstraction.IBaseDialogService, BaseDialogService>();

            //注册窗体
            containerRegistry.RegisterDialog<AlertDialog, ViewModels.AlertDialogViewModel>();
            containerRegistry.RegisterDialog<RegistView, ViewModels.RegistViewModel>();
            containerRegistry.RegisterDialog<LoginView, ViewModels.LoginViewModel>();
            containerRegistry.RegisterDialog<UserManage, ViewModels.UserManageViewModel>();
            containerRegistry.RegisterDialog<PasswordChange, ViewModels.PasswordChangeViewModel>();
            containerRegistry.RegisterDialog<LogView, ViewModels.LogViewViewModel>();
            containerRegistry.RegisterDialog<ConfirmationDialog, ViewModels.ConfirmationDialogViewModel>();
            containerRegistry.RegisterDialog<ErrorDialog, ViewModels.ErrorDialogViewModel>();
            containerRegistry.RegisterDialog<CustomDialog, ViewModels.CustomDialogViewModel>();
            //注入导航
            containerRegistry.RegisterForNavigation<Setting>();

        }
    }

}
