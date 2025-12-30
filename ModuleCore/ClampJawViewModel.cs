using Prism.Mvvm;
using Prism.Commands;
using System;
using SmarterMotion;
using System.Threading;
using System.Windows;
using System.Collections.Generic;

namespace ModuleCore.ViewModels
{
    public class ClampJawViewModel : BindableBase
    {
        //private RecipeManagerViewModel _parentViewModel;
        // 卡号
        private const ushort CardId = 0;

        // 传感器IO索引 
        private const ushort LeftSensor1Index = 62;  
        private const ushort LeftSensor2Index = 63;  
        private const ushort LeftSensor3Index = 64;  
        private const ushort RightSensor1Index = 65; 
        private const ushort RightSensor2Index = 66; 
        private const ushort RightSensor3Index = 67;

        public class ClampRule
        {
            public string Model { get; set; }
            public string LeftPattern { get; set; }
            public string RightPattern { get; set; }
            public string Description { get; set; }
        }

        // 夹爪型号规则
        public List<ClampRule> ClampRules { get; } = new List<ClampRule>
        {
            new ClampRule
            {
                Model = "S2",
                LeftPattern = "1 0 0",
                RightPattern = "1 0 0",
                Description = "左侧传感器1激活 + 右侧传感器1激活"
            },
            new ClampRule
            {
                Model = "S3",
                LeftPattern = "0 1 0",
                RightPattern = "0 1 0",
                Description = "左侧传感器2激活 + 右侧传感器2激活"
            },
            new ClampRule
            {
                Model = "S7",
                LeftPattern = "0 0 1",
                RightPattern = "0 0 1",
                Description = "左侧传感器3激活 + 右侧传感器3激活"
            },
            new ClampRule
            {
                Model = "未知",
                LeftPattern = "其他组合",
                RightPattern = "其他组合",
                Description = "未匹配任何标准型号"
            }
        };

        //public ClampJawViewModel(RecipeManagerViewModel parent)
        //{
        //    _parentViewModel = parent;
        //    ResetToDefaultCommand = new DelegateCommand(ResetToDefault);
        //    RefreshSignalCommand = new DelegateCommand(RefreshSignals);
        //    //RefreshSignalCommand = new DelegateCommand(ReadHardwareSignals);
        //    CheckMatchCommand = new DelegateCommand(CheckMatch);
        //    StartAutoCheckCommand = new DelegateCommand(StartAutoCheck);
        //    StopAutoCheckCommand = new DelegateCommand(StopAutoCheck);
        //}

        // 传感器状态
        private bool _leftSensor1;
        private bool _leftSensor2;
        private bool _leftSensor3;
        private bool _rightSensor1;
        private bool _rightSensor2;
        private bool _rightSensor3;

        public bool LeftSensor1
        {
            get => _leftSensor1;
            set => SetProperty(ref _leftSensor1, value, OnSensorsChanged);
        }

        public bool LeftSensor2
        {
            get => _leftSensor2;
            set => SetProperty(ref _leftSensor2, value, OnSensorsChanged);
        }

        public bool LeftSensor3
        {
            get => _leftSensor3;
            set => SetProperty(ref _leftSensor3, value, OnSensorsChanged);
        }

        public bool RightSensor1
        {
            get => _rightSensor1;
            set => SetProperty(ref _rightSensor1, value, OnSensorsChanged);
        }

        public bool RightSensor2
        {
            get => _rightSensor2;
            set => SetProperty(ref _rightSensor2, value, OnSensorsChanged);
        }

        public bool RightSensor3
        {
            get => _rightSensor3;
            set => SetProperty(ref _rightSensor3, value, OnSensorsChanged);
        }

        // 当前检测到的型号
        private string _detectedModel = "未知";
        public string DetectedModel
        {
            get => _detectedModel;
            set => SetProperty(ref _detectedModel, value);
        }

        // 配方要求的型号
        private string _requiredModel = "N/A";
        public string RequiredModel
        {
            get => _requiredModel;
            set => SetProperty(ref _requiredModel, value);
        }

        // 匹配状态
        private bool _isMatch;
        public bool IsMatch
        {
            get => _isMatch;
            set => SetProperty(ref _isMatch, value);
        }

        // 传感器组合描述
        private string _sensorPattern = "未设置";
        public string SensorPattern
        {
            get => _sensorPattern;
            set => SetProperty(ref _sensorPattern, value);
        }
        // 状态消息
        private string _statusMessage = "准备就绪";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        // 自动检查定时器
        private Timer _autoCheckTimer;
        private bool _isAutoChecking;
        public bool IsAutoChecking
        {
            get => _isAutoChecking;
            private set => SetProperty(ref _isAutoChecking, value);
        }
        // 命令
        public DelegateCommand ResetToDefaultCommand { get; }
        public DelegateCommand RefreshSignalCommand { get; }
        public DelegateCommand CheckMatchCommand { get; }
        public DelegateCommand StartAutoCheckCommand { get; }
        public DelegateCommand StopAutoCheckCommand { get; }
        /// <summary>
        /// 从硬件读取所有传感器信号
        /// </summary>
        public void ReadHardwareSignals()
        {
            try
            {
                StatusMessage = "正在读取传感器信号...";

                // 读取原始IO信号 (0-有信号, 1-无信号)
                short left1Raw = LTDMC.dmc_read_inbit(CardId, LeftSensor1Index);
                short left2Raw = LTDMC.dmc_read_inbit(CardId, LeftSensor2Index);
                short left3Raw = LTDMC.dmc_read_inbit(CardId, LeftSensor3Index);
                short right1Raw = LTDMC.dmc_read_inbit(CardId, RightSensor1Index);
                short right2Raw = LTDMC.dmc_read_inbit(CardId, RightSensor2Index);
                short right3Raw = LTDMC.dmc_read_inbit(CardId, RightSensor3Index);

                // 更新状态 (硬件返回0表示有信号)
                LeftSensor1 = (left1Raw == 0);
                LeftSensor2 = (left2Raw == 0);
                LeftSensor3 = (left3Raw == 0);
                RightSensor1 = (right1Raw == 0);
                RightSensor2 = (right2Raw == 0);
                RightSensor3 = (right3Raw == 0);

                StatusMessage = "传感器信号读取成功";
            }
            catch (Exception ex)
            {
                StatusMessage = $"传感器读取失败: {ex.Message}";
            }
        }
        private void OnSensorsChanged()
        {
            // 更新传感器组合描述
            SensorPattern = $"Left: {GetPattern(LeftSensor1, LeftSensor2, LeftSensor3)} " +
                          $"Right: {GetPattern(RightSensor1, RightSensor2, RightSensor3)}";

            // 检测型号
            DetectModel();

            // 检查匹配状态
            CheckMatch();
        }

        private string GetPattern(bool s1, bool s2, bool s3)
        {
            return $"{(s1 ? "1" : "0")} {(s2 ? "1" : "0")} {(s3 ? "1" : "0")}";
        }

        private void DetectModel()
        {
            // 逻辑规则:
            // Logic for S2: Left1 & Right1 active
            if (LeftSensor1 && !LeftSensor2 && !LeftSensor3 &&
                RightSensor1 && !RightSensor2 && !RightSensor3)
            {
                DetectedModel = "S2";
                return;
            }

            // Logic for S3: Left2 & Right2 active
            if (!LeftSensor1 && LeftSensor2 && !LeftSensor3 &&
                !RightSensor1 && RightSensor2 && !RightSensor3)
            {
                DetectedModel = "S3";
                return;
            }

            // Logic for S7: Left3 & Right3 active
            if (!LeftSensor1 && !LeftSensor2 && LeftSensor3 &&
                !RightSensor1 && !RightSensor2 && RightSensor3)
            {
                DetectedModel = "S7";
                return;
            }

            // Other combinations
            DetectedModel = "未知";
        }

        public void CheckMatch()
        {
            if (string.IsNullOrWhiteSpace(RequiredModel))
            {
                IsMatch = true; // 没有要求型号时视为匹配
                return;
            }

            if (string.IsNullOrWhiteSpace(DetectedModel) || DetectedModel == "未知")
            {
                IsMatch = false;
                return;
            }

            IsMatch = RequiredModel.Contains(DetectedModel);
        }

        public void RefreshSignals()
        {
            // 模拟传感器信号 //
            // 保留当前所需型号判断
            string currentReq = RequiredModel;

            // 75%概率随机设置匹配信号
            /*   bool shouldMatch = new Random().Next(0, 4) < 3;

               if (shouldMatch && !string.IsNullOrWhiteSpace(currentReq))
               {
                   // 设置与配方匹配的信号类型
                   if (currentReq.Contains("S2"))
                   {
                       LeftSensor1 = true; LeftSensor2 = false; LeftSensor3 = false;
                       RightSensor1 = true; RightSensor2 = false; RightSensor3 = false;
                   }
                   else if (currentReq.Contains("S3"))
                   {
                       LeftSensor1 = false; LeftSensor2 = true; LeftSensor3 = false;
                       RightSensor1 = false; RightSensor2 = true; RightSensor3 = false;
                   }
                   else if (currentReq.Contains("S7"))
                   {
                       LeftSensor1 = false; LeftSensor2 = false; LeftSensor3 = true;
                       RightSensor1 = false; RightSensor2 = false; RightSensor3 = true;
                   }
               }
               else
               {
                   // 随机设置
                   LeftSensor1 = new Random().Next(0, 2) > 0;
                   LeftSensor2 = new Random().Next(0, 2) > 0;
                   LeftSensor3 = new Random().Next(0, 2) > 0;
                   RightSensor1 = new Random().Next(0, 2) > 0;
                   RightSensor2 = new Random().Next(0, 2) > 0;
                   RightSensor3 = new Random().Next(0, 2) > 0;
               } */

            ReadHardwareSignals();
            UpdateRequiredModel();
            // 恢复所需型号
            RequiredModel = currentReq;
        }

        public void ResetToDefault()
        {
            // 重置为默认值
            LeftSensor1 = false;
            LeftSensor2 = false;
            LeftSensor3 = false;
            RightSensor1 = false;
            RightSensor2 = false;
            RightSensor3 = false;

            // 根据当前配方设置所需型号
            UpdateRequiredModel();
        }

        public void UpdateRequiredModel()
        {
            //// 从父ViewModel获取当前配方的名称
            //var recipeName = _parentViewModel.SelectedRecipe?.Name ?? "";

            //// 从配方名称中提取型号
            //if (recipeName.Contains("S2"))
            //{
            //    RequiredModel = "S2";
            //}
            //else if (recipeName.Contains("S3"))
            //{
            //    RequiredModel = "S3";
            //}
            //else if (recipeName.Contains("S7"))
            //{
            //    RequiredModel = "S7";
            //}
            //else
            //{
            //    RequiredModel = "N/A";
            //}

            // 检查匹配状态
            CheckMatch();
        }
        /// <summary>
        /// 同步执行夹爪状态检查（用于初始化流程）
        /// </summary>
        public bool CheckClampStatus()
        {
            try
            {
                StatusMessage = "初始化检查夹爪状态...";

                // 从硬件读取传感器信号
                ReadHardwareSignals();

                // 从配方获取要求型号
                UpdateRequiredModel();

                // 检查匹配状态
                CheckMatch();

                if (!IsMatch)
                {
                    var pattern = $"Left: {GetPattern(LeftSensor1, LeftSensor2, LeftSensor3)}" +
                                $" - Right: {GetPattern(RightSensor1, RightSensor2, RightSensor3)}";

                    StatusMessage = $"夹爪不匹配! 要求: {RequiredModel}, 检测: {DetectedModel}, 模式: {pattern}";
                    return false;
                }

                StatusMessage = "夹爪型号匹配";
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"夹爪检查失败: {ex.Message}";
                return false;
            }
        }
        // 启动自动检查
        private void StartAutoCheck()
        {
            if (_autoCheckTimer == null)
            {
                _autoCheckTimer = new Timer(_ =>
                {
                    // 在UI线程更新
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ReadHardwareSignals();
                        CheckMatch();
                    });
                }, null, 0, 1000); // 每秒检查一次

                IsAutoChecking = true;
                StatusMessage = "自动检测已启动";
            }
        }

        // 停止自动检查
        private void StopAutoCheck()
        {
            _autoCheckTimer?.Dispose();
            _autoCheckTimer = null;
            IsAutoChecking = false;
            StatusMessage = "自动检测已停止";
        }
    }
}
