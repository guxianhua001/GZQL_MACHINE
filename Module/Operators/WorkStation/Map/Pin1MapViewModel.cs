using Core.Extensions;
using Core.Services;
using Interfaces;
using MaterialDesignThemes.Wpf;
using ModuleCore.Models;
using Newtonsoft.Json;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Stations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.ViewModels
{
    public class Pin1MapViewModel : BaseMapViewModel<string>
    {
        private string _pinDisplayName = "Pin1路径规划";
        public string PinDisplayName
        {
            get => _pinDisplayName;
            set => SetProperty(ref _pinDisplayName, value);
        }
        //private readonly Task3 _task3;

        protected override int PinIndex => 1; // 标识当前处理Pin1

        public Pin1MapViewModel(
             IDialogService dialogService,
             RecipePool recipePool,
             AppConfig appConfig,
             TaskInstanceManager taskManager,
             LoginModel loginModel,
             IFileService fileService,
             ISnackbarMessageQueue snackbarQueue)
            : base(dialogService, recipePool, appConfig, taskManager, loginModel, fileService, snackbarQueue) // 基类已处理动态JSON
        {
            //_task3 = _taskManager.GetTask<Task3>();
        }
        // 可覆盖基类方法实现特殊逻辑
        protected override void ExecuteSaveRecipe()
        {
            base.ExecuteSaveRecipe();

        }
        protected override void ExecuteLoadRecipe()
        {
            base.ExecuteLoadRecipe();

        }

        protected override async void MoveToPoint(PointViewModel point)
        {
            base.MoveToPoint(point);
            Point targetPoint = new Point(point.X, point.Y);
            await Task.Run(() =>
            {
                //_task3.MoveToAxesXYPinDialingPoint(targetPoint);
            });
        }

    }
}
