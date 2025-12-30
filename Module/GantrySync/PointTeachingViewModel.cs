using Interfaces;
using Interfaces.Models;
using Interfaces.SharedInterfaces;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Framework.ViewModels
{
    public class PointTeachingViewModel : BindableBase
    {
        private readonly ICurrentSystemService _systemManager;
        private IGantrySyncService _currentSyncService;
        private string _currentSystemId;
        private readonly RecipePool _recipePool;
        private CancellationTokenSource _testCancellationTokenSource;
        public ObservableCollection<string> AvailableSystems { get; } =
            new ObservableCollection<string> { "系统1", "系统2" };

        private string _selectedSystem = "系统1";
        public string SelectedSystem
        {
            get => _selectedSystem;
            set
            {
                if (SetProperty(ref _selectedSystem, value))
                {
                    // 解析系统号：假设value格式为"系统N"
                    if (value.StartsWith("系统") && int.TryParse(value.Substring(2), out int systemId))
                    {
                        // 切换实际系统
                        _systemManager.SelectSystem(systemId);
                        _currentSystemId = systemId.ToString();
                    }
                    // 系统切换时重新加载点位
                    InitializePointsFromRecipe();
                }
            }
        }
        private double _moveSpeed = 50;
        public double MoveSpeed
        {
            get => _moveSpeed;
            set => SetProperty(ref _moveSpeed, value);
        }
        public ObservableCollection<PointDataModel> PointList { get; } =
            new ObservableCollection<PointDataModel>();

        private PointDataModel _selectedPoint;
        public PointDataModel SelectedPoint
        {
            get => _selectedPoint;
            set => SetProperty(ref _selectedPoint, value);
        }

        private bool _isTesting;
        public bool IsTesting
        {
            get => _isTesting;
            set => SetProperty(ref _isTesting, value);
        }

        private double _testInterval = 500; // ms
        public double TestInterval
        {
            get => _testInterval;
            set => SetProperty(ref _testInterval, Math.Max(100, value)); // 最小间隔100ms
        }

        public DelegateCommand AddPointCommand { get; }
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand TeachCommand { get; }
        public DelegateCommand MoveToCommand { get; }
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand ImportCommand { get; }
        public DelegateCommand ExportCommand { get; }
        public DelegateCommand StartTestCommand { get; }
        public DelegateCommand CancelTestCommand { get; }

        private DispatcherTimer _testTimer;
        private int _currentTestIndex = -1;

        public PointTeachingViewModel(
            ICurrentSystemService systemManager,
            RecipePool recipePool)
        {
            _systemManager = systemManager;
            _systemManager.SystemChanged += OnSystemChanged;
            _recipePool = recipePool;

            UpdateCurrentSystem();
            // 初始化时加载当前系统点位
            InitializePointsFromRecipe();

            // 命令初始化
            AddPointCommand = new DelegateCommand(AddPoint);
            DeleteCommand = new DelegateCommand(DeleteSelectedPoint, CanDeletePoint)
                .ObservesProperty(() => SelectedPoint);
            TeachCommand = new DelegateCommand(TeachCurrentPosition, CanTeach)
                .ObservesProperty(() => SelectedPoint);
            MoveToCommand = new DelegateCommand(MoveToSelectedPoint, CanMoveToPoint)
                .ObservesProperty(() => SelectedPoint);
            SaveCommand = new DelegateCommand(SavePoints);
            ImportCommand = new DelegateCommand(ImportPoints);
            ExportCommand = new DelegateCommand(ExportPoints);
            StartTestCommand = new DelegateCommand(StartTestSequence)
                .ObservesProperty(() => PointList.Count);
            CancelTestCommand = new DelegateCommand(CancelTest, CanCancelTest)
                .ObservesProperty(() => IsTesting);
        }

        private void OnSystemChanged(object sender, EventArgs e)
        {
            UpdateCurrentSystem();
            // 加载新系统的点位
            InitializePointsFromRecipe();
        }

        private void UpdateCurrentSystem()
        {
            _currentSyncService = _systemManager.CurrentSystem;
            _currentSystemId = _systemManager.CurrentSystem?.SystemId.ToString() ?? "1";
            SelectedSystem = $"系统{_currentSystemId}";
        }
        public void SelectSystem(int systemId)
        {
            // 通过系统管理器切换
            _systemManager.SelectSystem(systemId);
        }
        private void InitializePointsFromRecipe()
        {
            string currentSystem = $"龙门{_currentSystemId}";

            // 如果配方中有当前系统的点位数据，则加载
            if (_recipePool.CurrentRecipe.GantryPoints.ContainsKey(currentSystem))
            {
                PointList.Clear();
                foreach (var point in _recipePool.CurrentRecipe.GantryPoints[currentSystem])
                {
                    PointList.Add(point);
                }
            }
            // 否则创建默认点位并保存到配方
            else
            {
                for (int i = 1; i <= 5; i++)
                {
                    PointList.Add(new PointDataModel
                    {
                        Name = $"{SelectedSystem}-点位{i}",
                        UpperX = i * 100,
                        UpperY = i * 100 + 50,
                        LowerX = i * 100,
                        LowerY = i * 100 - 50
                    });
                }
                SavePointsToRecipe(); // 保存到配方
            }
        }
        // 保存点位到配方
        private void SavePointsToRecipe()
        {
            string systemKey = $"龙门{_currentSystemId}";

            // 使用更新方法代替直接设置
            _recipePool.CurrentRecipe.UpdateGantryPoints(
                systemKey,
                new ObservableCollection<PointDataModel>(PointList.Select(p => p.Clone()))
            );

            _recipePool.CurrentRecipe.SaveToFile();
        }

        private void LoadPointsForCurrentSystem()
        {
            try
            {
                PointList.Clear();
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config",
                                    $"龙门{SelectedSystem}_点位.points");

                if (!File.Exists(filePath))
                {
                    // 文件不存在时创建默认点位
                    for (int i = 1; i <= 5; i++)
                    {
                        PointList.Add(new PointDataModel
                        {
                            Name = $"{SelectedSystem}-点位{i}",
                            UpperX = i * 100,
                            UpperY = i * 100 + 50,
                            LowerX = i * 100,
                            LowerY = i * 100 - 50
                        });
                    }
                    return;
                }

                using (var reader = new StreamReader(filePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var parts = line.Split('|');
                        if (parts.Length == 5)
                        {
                            PointList.Add(new PointDataModel
                            {
                                Name = parts[0],
                                UpperX = double.TryParse(parts[1], out double x1) ? x1 : 0,
                                UpperY = double.TryParse(parts[2], out double y1) ? y1 : 0,
                                LowerX = double.TryParse(parts[3], out double x2) ? x2 : 0,
                                LowerY = double.TryParse(parts[4], out double y2) ? y2 : 0
                            });
                        }
                    }
                }

                if (PointList.Count > 0)
                    SelectedPoint = PointList[0];

                //ShowNotification($"已加载系统 '{SelectedSystem}' 的点位");
            }
            catch (Exception ex)
            {
                ShowNotification($"加载点位失败: {ex.Message}");
            }
        }

        private bool CanDeletePoint() => SelectedPoint != null;

        private bool CanTeach() => SelectedPoint != null && _currentSyncService != null;

        private bool CanMoveToPoint() => SelectedPoint != null && _currentSyncService != null;

        private bool CanCancelTest() => IsTesting;

        private void AddPoint()
        {
            var newPoint = new PointDataModel
            {
                Name = $"{SelectedSystem}-点位{PointList.Count + 1}",
                UpperX = SelectedPoint?.UpperX ?? 0,
                UpperY = SelectedPoint?.UpperY ?? 0,
                LowerX = SelectedPoint?.LowerX ?? 0,
                LowerY = SelectedPoint?.LowerY ?? 0
            };

            PointList.Add(newPoint);
            SelectedPoint = newPoint;
        }

        private void DeleteSelectedPoint()
        {
            if (SelectedPoint == null) return;

            int index = PointList.IndexOf(SelectedPoint);
            PointList.Remove(SelectedPoint);

            // 选择下一个或上一个点
            if (PointList.Count > 0)
            {
                SelectedPoint = index < PointList.Count ? PointList[index] : PointList[^1];
            }
        }

        private void TeachCurrentPosition()
        {
            if (_currentSyncService == null || SelectedPoint == null) return;

            var upperPos = _currentSyncService.GetUpperGantryPosition();
            var lowerPos = _currentSyncService.GetLowerGantryPosition();

            SelectedPoint.UpperX = upperPos.X;
            SelectedPoint.UpperY = upperPos.Y;
            SelectedPoint.LowerX = lowerPos.X;
            SelectedPoint.LowerY = lowerPos.Y;

            ShowNotification($"当前位置已示教到 '{SelectedPoint.Name}'");
        }

        private async void MoveToSelectedPoint()
        {
            if (_currentSyncService == null || SelectedPoint == null) return;

            try
            {
                ShowNotification($"开始移动到 '{SelectedPoint.Name}'...");

                await _currentSyncService.MoveToTarget(
                    new PointF((float)SelectedPoint.UpperX, (float)SelectedPoint.UpperY),
                    new PointF((float)SelectedPoint.LowerX, (float)SelectedPoint.LowerY),
                    _moveSpeed
                );
            }
            catch (Exception ex)
            {
                ShowNotification($"移动失败: {ex.Message}");
            }
        }

        private void SavePoints()
        {
            try
            {
                SavePointsToRecipe(); // 保存到配方

                // 同时保存到独立文件（可选）
                string filePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Config",
                    $"龙门{SelectedSystem}_点位.points");

                using (var writer = new StreamWriter(filePath))
                {
                    foreach (var point in PointList)
                    {
                        writer.WriteLine($"{point.Name}|{point.UpperX}|{point.UpperY}|{point.LowerX}|{point.LowerY}");
                    }
                }

                ShowNotification($"点位已保存到配方和文件: {filePath}");
            }
            catch (Exception ex)
            {
                ShowNotification($"保存失败: {ex.Message}");
            }
        }

        private void ImportPoints()
        {
            try
            {
                string filePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config",
                $"龙门{SelectedSystem}_点位.points");

                if (!File.Exists(filePath)) return;

                PointList.Clear();
                using (var reader = new StreamReader(filePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var parts = line.Split('|');
                        if (parts.Length == 5)
                        {
                            PointList.Add(new PointDataModel
                            {
                                Name = parts[0],
                                UpperX = double.Parse(parts[1]),
                                UpperY = double.Parse(parts[2]),
                                LowerX = double.Parse(parts[3]),
                                LowerY = double.Parse(parts[4])
                            });
                        }
                    }
                }
                if (PointList.Count > 0)
                    SelectedPoint = PointList[0];
                // 更新配方
                SavePointsToRecipe();

                ShowNotification($"从 {filePath} 导入 {PointList.Count} 个点位");
            }
            catch (Exception ex)
            {
                ShowNotification($"导入失败: {ex.Message}");
            }
        }

        private void ExportPoints()
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "导出点位数据",
                    Filter = "点位文件 (*.points)|*.points|所有文件 (*.*)|*.*",
                    FileName = $"龙门点位_{SelectedSystem}_{DateTime.Now:yyyyMMdd}.points",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var writer = new StreamWriter(saveDialog.FileName))
                    {
                        writer.WriteLine("点位名称;上龙门X;上龙门Y;下龙门X;下龙门Y");
                        foreach (var point in PointList)
                        {
                            writer.WriteLine($"{point.Name};{point.UpperX};{point.UpperY};{point.LowerX};{point.LowerY}");
                        }
                    }

                    ShowNotification($"点位已导出到: {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"导出失败: {ex.Message}");
            }
        }

        private async void StartTestSequence()
        {
            if (IsTesting || PointList.Count == 0) return;

            IsTesting = true;
            _currentTestIndex = 0;
            _testCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _testCancellationTokenSource.Token;

            try
            {
                // 在新的任务中执行测试序列
                await Task.Run(async () =>
                {
                    for (int i = 0; i < PointList.Count; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        // 在UI线程更新选中的点
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _currentTestIndex = i;
                            SelectedPoint = PointList[i];
                        });

                        // 显示通知（如果需要在UI线程显示）
                        ShowNotification($"测试中: {PointList[i].Name} ({i + 1}/{PointList.Count})");

                        // 移动到当前点
                        var point = PointList[i];
                        if (_currentSyncService != null)
                        {
                            await _currentSyncService.MoveToTarget(
                                new PointF((float)point.UpperX, (float)point.UpperY),
                                new PointF((float)point.LowerX, (float)point.LowerY),
                                MoveSpeed
                            );
                        }

                        // 等待测试间隔
                        await Task.Delay((int)TestInterval, cancellationToken);
                    }
                }, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    ShowNotification("测试已取消");
                }
                else
                {
                    ShowNotification("测试完成");
                }
            }
            catch (TaskCanceledException)
            {
                ShowNotification("测试已取消");
            }
            catch (Exception ex)
            {
                ShowNotification($"测试中断: {ex.Message}");
            }
            finally
            {
                IsTesting = false;
                _testCancellationTokenSource = null;
            }
        }

        private void TestComplete()
        {
            _testTimer?.Stop();
            IsTesting = false;
            ShowNotification("测试完成");
        }

        private void CancelTest()
        {
            if (IsTesting && _testCancellationTokenSource != null)
            {
                IsTesting = false;
                _testCancellationTokenSource.Cancel();
                _currentSyncService?.StopAllMotion();
                ShowNotification("测试已取消");
            }
        }

        private void ShowNotification(string message)
        {
            // 实际实现中应该显示在UI通知区域
            MessageBox.Show($"[{DateTime.Now:HH:mm:ss}] {message}", "通知", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}