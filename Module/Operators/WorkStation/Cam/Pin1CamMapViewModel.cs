using Core.Extensions;
using Core.Services;
using Interfaces;
using ModuleCore.Models;
using Prism.Services.Dialogs;
using Stations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.ViewModels
{
    public class Pin1CamMapViewModel : CamHighCmpViewModel<string>
    {
        private string _pinDisplayName = "Pin1飞拍位置";
        public string PinDisplayName
        {
            get => _pinDisplayName;
            set => SetProperty(ref _pinDisplayName, value);
        }
        protected override int CamIndex => 1; // 标识当前处理Pin1
        //private readonly Task2 _task2;
        public Pin1CamMapViewModel(
          IDialogService dialogService,
          RecipePool recipePool,
          AppConfig appConfig,
          TaskInstanceManager taskManager,
          LoginModel loginModel)
         : base(dialogService, recipePool, appConfig, taskManager, loginModel) // 基类已处理动态JSON
        {
            //_task2 = _taskManager.GetTask<Task2>();
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
                //_task2.GantryStation_MoveToVisionInspPosn1(point.X, point.Y);
            });
        }
    }
}
