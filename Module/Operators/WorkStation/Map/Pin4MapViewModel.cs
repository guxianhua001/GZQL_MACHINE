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
    public class Pin4MapViewModel : BaseMapViewModel<string>
    {
        private string _pinDisplayName = "Pin4路径规划";
        public string PinDisplayName
        {
            get => _pinDisplayName;
            set => SetProperty(ref _pinDisplayName, value);
        }
        //private readonly Task6 _task6;

        protected override int PinIndex => 4; // 标识当前处理Pin1

        public Pin4MapViewModel(
             IDialogService dialogService,
             RecipePool recipePool,
             AppConfig appConfig,
             TaskInstanceManager taskManager,
             LoginModel loginModel,
             IFileService fileService,
             ISnackbarMessageQueue snackbarQueue)
            : base(dialogService, recipePool, appConfig, taskManager, loginModel, fileService, snackbarQueue) // 基类已处理动态JSON
        {
            //_task6 = _taskManager.GetTask<Task6>();
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
                //_task6.MoveToAxesXYPinDialingPoint(targetPoint);
            });
        }

    }
}
