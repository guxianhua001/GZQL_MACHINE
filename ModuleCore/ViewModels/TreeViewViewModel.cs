using Framework.Mvvm;
using ModuleCore.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace ModuleCore.ViewModels
{
    public sealed class TreeNode
    {
        public string Name { get; set; }
        public string Path { get; set; } // 添加路径属性
        public ObservableCollection<TreeNode> Children { get; set; }

        public TreeNode(string name)
        {
            Name = name;
            Children = new ObservableCollection<TreeNode>();
    }
    }
    public sealed class Movie
    {
        public Movie(string name, string director)
        {
            Name = name;
            Director = director;
        }

        public string Name { get; }

        public string Director { get; }
    }
    public sealed class MovieCategory
    {
        public MovieCategory(string name, params Movie[] movies)
        {
            Name = name;
            Movies = new ObservableCollection<Movie>(movies);
        }

        public string Name { get; }

        public ObservableCollection<Movie> Movies { get; }
    }
    public class TreeViewViewModel : RegionViewModelBase
    {
        private string _title = "TreeView";

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }
        private object? _selectedItem;
        public object? SelectedItem
        {
            get => _selectedItem;
            set
            {
                SetProperty(ref _selectedItem, value);
                NavigationTreeView_SelectedItemChanged(_selectedItem);
            }
        }
        public ObservableCollection<MovieCategory> MovieCategories { get; set; }
        public ObservableCollection<TreeNode> TreeData { get; set; }
        private readonly IEventAggregator _eventAggregator;

        public TreeViewViewModel(IRegionManager regionManager): base(regionManager)
        {
            _regionManager = regionManager;

            TreeData = new ObservableCollection<TreeNode>
            {
                new TreeNode("Root 1")
                {
                    Children = new ObservableCollection<TreeNode>
                    {
                        new TreeNode("主流线")
                        {
                            Path = "Root1/MainLine",
                            Children = new ObservableCollection<TreeNode>
                            {
                                new TreeNode("手动调试") { Path = "Root1/MainLine/ManualTest" },
                                new TreeNode("轴调试"){ Path = "Root1/MainLine/Axis" },
                                new TreeNode("气缸调试"){ Path = "Root1/MainLine/Cylinder" },
                                new TreeNode("位置参数"){ Path = "Root1/MainLine/Param" }
                            }
                        },
                        new TreeNode("龙门检测左侧模组")
                        {
                             Path = "Root1/GantryModule",
                            Children = new ObservableCollection<TreeNode>
                            {
                                new TreeNode("手动调试"){ Path = "Root1/GantryModule/ManualTest" },
                                new TreeNode("轴调试"){ Path = "Root1/GantryModule/Axis" },
                                new TreeNode("位置参数"){ Path = "Root1/GantryModule/Param" }
                            }
                        },
                        new TreeNode("龙门检测右侧模组")
                        {
                            Path = "Root1/ChipCheckModule1",
                            Children = new ObservableCollection<TreeNode>
                            {
                                new TreeNode("手动调试") { Path = "Root1/ChipCheckModule1/ManualTest" },
                                new TreeNode("轴调试") { Path = "Root1/ChipCheckModule1/Axis" },
                                new TreeNode("位置参数"){ Path = "Root1/ChipCheckModule1/Param" },
                            }
                        },
                    }
                }
            };
        }

        //导航
        private readonly IRegionManager _regionManager;

        private DelegateCommand<string> _NavigateCommand;

        public DelegateCommand<string> NavigateCommand =>
             _NavigateCommand ??= new DelegateCommand<string>(ExecuteNavigateCommand);
        private void ExecuteNavigateCommand(string navigatePath)
        {
            if (string.IsNullOrEmpty(navigatePath))
                return;

            _regionManager.RequestNavigate("TreeRegion", navigatePath);
        }
        private void NavigationTreeView_SelectedItemChanged(object args)
        {
            var selectedItem = args as TreeNode;
            if (selectedItem != null)
            {
                switch (selectedItem.Path)
                {
                    case "View A":
                        //_navigationService.Navigate("ViewA"); // 这里的"ViewA"应该与您在模块中注册的视图名称相匹配
                        break;
                        //主流线
                    case "Root1/MainLine/ManualTest":
                        _regionManager.RequestNavigate("TreeRegion", "LoaderStationView");
                        break;
                    case "Root1/MainLine/Axis":
                        _regionManager.RequestNavigate("TreeRegion", "LoaderStationAxesView"); 
                        break;
                    case "Root1/MainLine/Cylinder":
                        _regionManager.RequestNavigate("TreeRegion", "LoaderStationCylinderView");
                        break;
                    case "Root1/MainLine/Param":
                        _regionManager.RequestNavigate("TreeRegion", "LoaderStationPositionView");
                        break;
                        //龙门检测左侧模组
                    case "Root1/GantryModule/ManualTest":
                        _regionManager.RequestNavigate("TreeRegion", "GantryStationsView");
                        break;
                    case "Root1/GantryModule/Axis":
                        _regionManager.RequestNavigate("TreeRegion", "GantryStationAxesView");
                        break;
                    case "Root1/GantryModule/Cylinder":
                        _regionManager.RequestNavigate("TreeRegion", "GantryStationCylinderView");
                        break;
                    case "Root1/GantryModule/Param":
                        _regionManager.RequestNavigate("TreeRegion", "GantryStationPositionView");
                        break;
                        //龙门检测右侧模组
                    case "Root1/ChipCheckModule1/ManualTest":
                        _regionManager.RequestNavigate("TreeRegion", "CheckStation1View");
                        break;
                    case "Root1/ChipCheckModule1/MapSetting":
                        _regionManager.RequestNavigate("TreeRegion", "Pin1MapView");
                        break;
                    case "Root1/ChipCheckModule1/Axis":
                        _regionManager.RequestNavigate("TreeRegion", "CheckStation1AxisView");
                        break;
                    case "Root1/ChipCheckModule1/Cylinder":
                        _regionManager.RequestNavigate("TreeRegion", "CheckStation1CylinderView");
                        break;
                    case "Root1/ChipCheckModule1/Param":
                        _regionManager.RequestNavigate("TreeRegion", "CheckStation1PositionView");
                        break;
                    case "Root1/ChipCheckModule1/Camera":
                        _regionManager.RequestNavigate("TreeRegion", "Pin1CamMapView");
                        break;
                }
            }
        }
        //窗体关闭
        public virtual void RaiseRequestClose(IDialogResult dialogResult)
        {
            RequestClose?.Invoke(dialogResult);
        }
        //窗体打开
        public void OnDialogOpened(IDialogParameters parameters)
        {

        }
        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
        }

        #region Members
        /* TreeData = new ObservableCollection<TreeNode>
             {
                 new TreeNode("Root 1")
         {
             Children = new ObservableCollection<TreeNode>
                     {
                         new TreeNode("Child 1.1")
                         {
                             Children = new ObservableCollection<TreeNode>
                             {
                                 new TreeNode("Grandchild 1.1.1"),
                                 new TreeNode("Grandchild 1.1.2")
                             }
                         },
                         new TreeNode("Child 1.2")
                         {
                             Children = new ObservableCollection<TreeNode>
                             {
                                 new TreeNode("Grandchild 1.2.1"),
                                 new TreeNode("Grandchild 1.2.2")
                             }
                         },
                         new TreeNode("Child 1.3")
                         {
                             Children = new ObservableCollection<TreeNode>
                             {
                                 new TreeNode("Grandchild 1.3.1"),
                                 new TreeNode("Grandchild 1.3.2")

                             }
                         }
                     }
                 }
     };*/
        #endregion
    }
}
