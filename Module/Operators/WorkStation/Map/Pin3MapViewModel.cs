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
    public class Pin3MapViewModel : BaseMapViewModel<string>
    {
        private string _pinDisplayName = "Pin3路径规划";
        public string PinDisplayName
        {
            get => _pinDisplayName;
            set => SetProperty(ref _pinDisplayName, value);
        }
        //private readonly Task5 _task5;

        protected override int PinIndex => 3; // 标识当前处理Pin1

        public Pin3MapViewModel(
             IDialogService dialogService,
             RecipePool recipePool,
             AppConfig appConfig,
             TaskInstanceManager taskManager,
             LoginModel loginModel,
             IFileService fileService,
             ISnackbarMessageQueue snackbarQueue)
            : base(dialogService, recipePool, appConfig, taskManager, loginModel, fileService, snackbarQueue) // 基类已处理动态JSON
        {
            //_task5 = _taskManager.GetTask<Task5>();
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
                //_task5.MoveToAxesXYPinDialingPoint(targetPoint);
            });
        }

    }
}
