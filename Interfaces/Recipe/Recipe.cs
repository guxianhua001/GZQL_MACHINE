using Interfaces.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Interfaces
{
    /// <summary>
    /// 配方类，用于存储配方信息。
    /// </summary>
    public class Recipe : BindableBase
    {
        public int Version { get; set; } = 1; // 添加版本号
        private string _name = "NEW Recipe";
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        public DateTime CreateDate { get; set; } = DateTime.Now;
        // 在ViewModel中维护最近文件列表
        public ObservableCollection<string> RecentFiles { get; } = new();

        public string FilePath { get; set; }
        // 保存到文件（兼容旧格式）
        public void SaveToFile()
        {
            if (string.IsNullOrEmpty(FilePath))
            {
               string _defaultRecipeDirectory = Path.Combine(
                                AppDomain.CurrentDomain.BaseDirectory,
                                "Recipes");
                FilePath = $"{_defaultRecipeDirectory}/{DateTime.Now:yyyyMMddHHmmss}.recipe";
            }
            var filteredPath = Regex.Replace(FilePath, @"[\p{C}]", "");
            File.WriteAllText(filteredPath, JsonConvert.SerializeObject(this, Formatting.Indented));

        }
        // 需要实现无参构造函数
        public Recipe()
        {
            _gantryPoints.CollectionChanged += OnGantryPointsDictionaryChanged;
        }
        // 其他属性...
        public Recipe Clone()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                WriteIndented = true,
                IncludeFields = true // 如果需要序列化字段
            };
            var json = System.Text.Json.JsonSerializer.Serialize(this, options);
            return System.Text.Json.JsonSerializer.Deserialize<Recipe>(json, options);
        }

        #region 生产参数
        /// <summary>
        /// 出料叠数 
        /// </summary>
        private int _stackCount = 1;  // 默认1层
        public int StackCount
        {
            get => _stackCount;
            set => SetProperty(ref _stackCount, Math.Clamp(value, 1, 10)); // 值域安全保证
        }

        /// <summary>
        /// 读码器选项索引
        /// </summary>
        private int _codingSelectIndex = 1;  // 默认1
        public int CodingSelectIndex
        {
            get => _codingSelectIndex;
            set => SetProperty(ref _codingSelectIndex, value); 
        }
        /// <summary>
        /// 基高1，下压高度1，基高2，下压高度2，基高3，下压高度3，基高4，下压高度4
        /// </summary>
        private float _baseHeight1;
        public float BaseHeight1
        {
            get => _baseHeight1;
            set => SetProperty(ref _baseHeight1, value);
        }
        private float _downHeight1 = -1.0f;
        public float DownHeight1
        {
            get => _downHeight1;
            set => SetProperty(ref _downHeight1, ValidateDownHeight(value));
        }
        private float _baseHeight2;
        public float BaseHeight2
        {
            get => _baseHeight2;
            set => SetProperty(ref _baseHeight2, value);
        }
        private float _downHeight2 = -1.0f;
        public float DownHeight2
        {
            get => _downHeight2;
            set => SetProperty(ref _downHeight2, ValidateDownHeight(value));
        }
        private float _baseHeight3;
        public float BaseHeight3
        {
            get => _baseHeight3;
            set => SetProperty(ref _baseHeight3, value);
        }
        private float _downHeight3 = -1.0f;
        public float DownHeight3
        {
            get => _downHeight3;
            set => SetProperty(ref _downHeight3, ValidateDownHeight(value));
        }
        private float _baseHeight4;
        public float BaseHeight4
        {
            get => _baseHeight4;
            set => SetProperty(ref _baseHeight4, value);
        }
        private float _downHeight4 = -1.0f;
        public float DownHeight4
        {
            get => _downHeight4;
            set => SetProperty(ref _downHeight4, ValidateDownHeight(value));
        }
        // 下降高度验证方法
        private float ValidateDownHeight(float value)
        {
            if (value > 0f) return -0f; // 限制为负数（实际存储负值）
            value = -Math.Abs(value);

            // 限制范围在负0-5mm之间
            if (value < -5f) value = -5f;
            if (value > -0f) value = -0f;
            // 保留一位小数
            return (float)Math.Round(value, 1);
        }
        #endregion

        #region 力控参数
        //力控参数
        private double _xMoveDistance;//X轴拨针距离
        private double _yMoveDistance;//Y轴拨针距离
        private bool _isReturnDialEnabled = false;
        private double _returnDialDistance;//回拨距离
        private double _moveSpeed;//轴拨针速度
        private double _yMoveHomeSpeed;//Y轴回原点速度
        private double _forwardHomeTorque;//正向寻针扭矩
        private double _forwardTorqueTarget;//向目标扭矩
        private double _forwardMaxTorque;//向最大扭矩
        private double _negativeHomeTorque;//负向寻针扭矩
        private double _negativeTorqueTarget;//负向目标扭矩
        private double _negativeMaxTorque;//负向最大扭矩
        private int _frequency = 2000;//采样频率
        public double XMoveDistance
        {
            get => _xMoveDistance;
            set
            {
                if (value > 4)
                    throw new ArgumentException("位移值不能大于4mm!");
                SetProperty(ref _xMoveDistance, value);
            }
        }
        public double YMoveDistance
        {
            get => _yMoveDistance;
            set
            {
                if (value > 4)
                    throw new ArgumentException("位移值不能大于4mm!");
                SetProperty(ref _yMoveDistance, value);
            }
        }
        public double MoveSpeed
        {
            get => _moveSpeed;
            set
            {
                if (value > 5)
                    throw new ArgumentException("拨针速度不能大于5mm/s!");
                SetProperty(ref _moveSpeed, value);
            }
        }
        public bool IsReturnDialEnabled
        {
            get => _isReturnDialEnabled;
            set
            {
                if (_isReturnDialEnabled != value)
                {
                    _isReturnDialEnabled = value;
                    RaisePropertyChanged();  // 确保有通知

                    // 可选：禁用时重置回拨距离
                    //if (!value) ReturnDialDistance = 0;
                }
            }
        }

        public double ReturnDialDistance
        {
            get => _returnDialDistance;
            set
            {
                if (value > 4)
                    throw new ArgumentException("位移值不能大于4mm!");
                SetProperty(ref _returnDialDistance, value);
            }
        }
        public int Frequency
        {
            get => _frequency;
            set
            {
                if (value < 1000  || value > 30000)
                    throw new ArgumentException("采集频率范围1000-30000 Hz!");
                SetProperty(ref _frequency, value);
            }
        }
        public double MoveHomeSpeed
        {
            get => _yMoveHomeSpeed;
            set
            {
                if (value > 20)
                    throw new ArgumentException("拨针速度不能大于20mm/s!");
                SetProperty(ref _yMoveHomeSpeed, value);
            }
        }
        public double ForwardHomeTorque
        {
            get => _forwardHomeTorque;
            set
            {
                if (value < 0 || value > 2)
                    throw new ArgumentException("正向零点接触力不能为负值或大于2!");
                SetProperty(ref _forwardHomeTorque, value);
            }
        }
        public double ForwardTorqueTarget
        {
            get => _forwardTorqueTarget;
            set
            {
                if (value < 2 || value > 6)
                    throw new ArgumentException("正向拨针力不能小于2或大于6!");
                SetProperty(ref _forwardTorqueTarget, value);
            }
        }
        public double ForwardMaxTorque
        {
            get => _forwardMaxTorque;
            set
            {
                if (value < 0 || value > 7)
                    throw new ArgumentException("正向拨针力最大值不能为负值或大于7!");
                SetProperty(ref _forwardMaxTorque, value);
            }
        }
        public double NegativeHomeTorque
        {
            get => _negativeHomeTorque;
            set
            {
                if (value > 0 && value < -2)
                    throw new ArgumentException("负向零点接触力不能为正值或小于-2!");
                SetProperty(ref _negativeHomeTorque, value);
            }
        }
        public double NegativeTorqueTarget
        {
            get => _negativeTorqueTarget;
            set
            {
                if (value > -2 && value < -6)
                    throw new ArgumentException("负向拨针力不能大于-2或小于-6!");
                SetProperty(ref _negativeTorqueTarget, value);
            }
        }
        public double NegativeMaxTorque
        {
            get => _negativeMaxTorque;
            set
            {
                if (value > 0 && value < -7)
                    throw new ArgumentException("负向拨针力最大值不能为正值或小于-7!");
                SetProperty(ref _negativeMaxTorque, value);
            }
        }
        // IDataErrorInfo 实现
        public string Error => null;
        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(XMoveDistance):
                        return XMoveDistance < 0 ? "位移值无效" : null;
                    // 其他属性验证...
                    default:
                        return null;
                }
            }
        }
        #endregion

        #region 相机配置和编码器配置
        //新增相机拍照位置
        public Dictionary<int, string> _cambaseConfigs = new();
        public Dictionary<int, string> _cameraconfigs = new();
        //编码器配置位置
        public Dictionary<int, string> _encoderconfigs = new();
        //相机特征点配置
        public Dictionary<int, string> _cameraMarkConfigs = new();
        //相机特征点编码器配置
        public Dictionary<int, string> _cameraMarkEncoderconfigs = new();
        // 动态存储所有Pin拍照点集合
        public Dictionary<int, ObservableCollection<PointViewModel>> _camPoints = new();
        // 相机拍照点的Y偏移量数组
        public double[] deltaYArray;
        //相机特征点拍照点的Y偏移量数组
        public double[] deltaMarkYArray;
        public void SetCamConfig(int camIndex, bool isBaseConfig, string value)
        {
            if (isBaseConfig)
                _cambaseConfigs[camIndex] = value;
            else
                _cameraconfigs[camIndex] = value;

            // 触发细粒度通知
            RaisePropertyChanged(GetCamConfigPropertyName(camIndex, isBaseConfig));
            SyncCamPointsFromJson(camIndex);
        }
        // 生成动态属性名称（例如 Pin1BaseConfig、Pin2Config）
        private string GetCamConfigPropertyName(int pinIndex, bool isBaseConfig)
        {
            return $"Cam{pinIndex}{(isBaseConfig ? "Encoder" : "")}Config";
        }
        public string GetCamConfig(int camIndex, bool isBaseConfig)
        {
            return isBaseConfig ?
                (_cambaseConfigs.TryGetValue(camIndex, out var encoderJson) ? encoderJson : "") :
                (_cameraconfigs.TryGetValue(camIndex, out var baseJson) ? baseJson : "");
        }
        /// <summary>
        /// 获取指定Pin的点集合
        /// </summary>
        public ObservableCollection<PointViewModel> GetCamPoints(int camIndex)
        {
            if (!_camPoints.ContainsKey(camIndex))
                _camPoints[camIndex] = new ObservableCollection<PointViewModel>();
            return _camPoints[camIndex];
        }
        // 同步JSON到点集合
        private void SyncCamPointsFromJson(int camIndex)
        {
            try
            {
                var json = _cameraconfigs[camIndex];
                if (string.IsNullOrEmpty(json)) return;

                var data = JsonConvert.DeserializeObject<JObject>(json);
                var points = data["Points"]?.ToObject<List<PointViewModel>>();

                if (points != null)
                    _camPoints[camIndex] = new ObservableCollection<PointViewModel>(points);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pin{camIndex} JSON同步失败: {ex.Message}");
            }
        }
        // 拍照位编码器配置字典
        public Dictionary<int, string> _encoderConfigs = new();

        // 编码器配置操作方法
        public void SetEncoderConfig(int camIndex, string value)
        {
            _encoderConfigs[camIndex] = value;
            RaisePropertyChanged(nameof(_encoderconfigs));
        }

        public string GetEncoderConfig(int camIndex)
        {
            return _encoderConfigs.TryGetValue(camIndex, out var json) ? json : "";
        }
        #endregion

        #region Pin配置

        // 动态存储所有Pin的基础配置JSON
        public Dictionary<int, string> _basePinConfigs = new();
        // 动态存储所有Pin的详细配置JSON
        public Dictionary<int, string> _pinConfigs = new();
        // 动态存储所有Pin的点集合
        public Dictionary<int, ObservableCollection<PointViewModel>> _pinPoints = new();
        // 添加显式方法替代索引器
        public string GetPinConfig(int pinIndex, bool isBaseConfig)
        {
            return isBaseConfig ?
                (_basePinConfigs.TryGetValue(pinIndex, out var baseJson) ? baseJson : "") :
                (_pinConfigs.TryGetValue(pinIndex, out var pinJson) ? pinJson : "");
        }

        public void SetPinConfig(int pinIndex, bool isBaseConfig, string value)
        {
            if (isBaseConfig)
                _basePinConfigs[pinIndex] = value;
            else
                _pinConfigs[pinIndex] = value;

            // 触发细粒度通知
            RaisePropertyChanged(GetPinConfigPropertyName(pinIndex, isBaseConfig));
            SyncPointsFromJson(pinIndex);
        }
        // 生成动态属性名称（例如 Pin1BaseConfig、Pin2Config）
        private string GetPinConfigPropertyName(int pinIndex, bool isBaseConfig)
        {
            return $"Pin{pinIndex}{(isBaseConfig ? "Base" : "")}Config";
        }

        /// <summary>
        /// 获取所有配置的JSON摘要（用于调试或UI展示）
        /// </summary>
        public string Configs => JsonConvert.SerializeObject(new
        {
            BasePins = _basePinConfigs,
            Pins = _pinConfigs
        }, Formatting.Indented);

        /// <summary>
        /// 获取指定Pin的点集合
        /// </summary>
        public ObservableCollection<PointViewModel> GetPinPoints(int pinIndex)
        {
            if (!_pinPoints.ContainsKey(pinIndex))
                _pinPoints[pinIndex] = new ObservableCollection<PointViewModel>();
            return _pinPoints[pinIndex];
        }

        // 同步JSON到点集合
        private void SyncPointsFromJson(int pinIndex)
        {
            try
            {
                var json = _pinConfigs[pinIndex];
                if (string.IsNullOrEmpty(json)) return;

                var data = JsonConvert.DeserializeObject<JObject>(json);
                var points = data["Points"]?.ToObject<List<PointViewModel>>();

                if (points != null)
                    _pinPoints[pinIndex] = new ObservableCollection<PointViewModel>(points);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pin{pinIndex} JSON同步失败: {ex.Message}");
            }
        }
        #endregion

        #region 相机mark配置
        public void SetCameraMarkConfig(int camIndex, string value)
        {
            // 使用特殊键名存储相机Mark点配置（格式：Cam[Index]_Marks）
            _cameraMarkConfigs[camIndex] = value;
            RaisePropertyChanged($"CameraMarkConfig_{camIndex}");
        }
        public string GetCameraMarkConfig(int camIndex)
        {
            // 使用特殊键名读取相机Mark点配置
            return _cameraMarkConfigs.TryGetValue(camIndex, out var json) ? json : "";
        }
        // 编码器配置操作方法
        public void SetCameraMarkEncoderConfig(int camIndex, string value)
        {
            _cameraMarkEncoderconfigs[camIndex] = value;
            RaisePropertyChanged(nameof(_cameraMarkEncoderconfigs));
        }
        public string GetCameraMarkEncoderConfig(int camIndex)
        {
            // 读取相机Mark点编码器配置
            return _cameraMarkEncoderconfigs.TryGetValue(camIndex, out var json) ? json : "";
        }
        #endregion

        #region 拍照点位
        // 添加龙门点位字典
        //public Dictionary<string, ObservableCollection<PointDataModel>> GantryPoints { get; set; }
        //     = new Dictionary<string, ObservableCollection<PointDataModel>>();
        private ObservableDictionaryExt<string, ObservableCollection<PointDataModel>> _gantryPoints
                 = new ObservableDictionaryExt<string, ObservableCollection<PointDataModel>>();

        public ObservableDictionaryExt<string, ObservableCollection<PointDataModel>> GantryPoints
        {
            get => _gantryPoints;
            set => SetProperty(ref _gantryPoints, value);
        }
        public event EventHandler<GantryPointsChangedEventArgs> GantryPointsChanged;
        // 处理字典级别的变更
        private void OnGantryPointsDictionaryChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 添加新项目时注册事件
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<KeyValuePair<string, ObservableCollection<PointDataModel>>>())
                {
                    item.Value.CollectionChanged += OnGantryCollectionChanged;
                    //OnPointsUpdated(item.Key, item.Value);
                }
            }

            // 移除项目时解注册事件
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<KeyValuePair<string, ObservableCollection<PointDataModel>>>())
                {
                    item.Value.CollectionChanged -= OnGantryCollectionChanged;
                }
            }

            // 触发PropertyChanged通知
            RaisePropertyChanged(nameof(GantryPoints));
        }

        // 统一的点位更新方法
        public void UpdateGantryPoints(string systemName, ObservableCollection<PointDataModel> points)
        {
            // 1. 如果已存在集合，先移除事件处理
            if (_gantryPoints.TryGetValue(systemName, out var existing))
            {
                existing.CollectionChanged -= OnGantryCollectionChanged;
            }

            // 2. 添加新集合到字典
            var newCollection = points ?? new ObservableCollection<PointDataModel>();
            newCollection.CollectionChanged += OnGantryCollectionChanged;
            _gantryPoints[systemName] = newCollection;

            // 3. 手动触发通知
            OnPointsUpdated(systemName, newCollection);
        }

        // 集合级别变更处理
        private void OnGantryCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is ObservableCollection<PointDataModel> collection)
            {
                var systemKey = _gantryPoints.FirstOrDefault(kvp => kvp.Value == collection).Key;
                if (!string.IsNullOrEmpty(systemKey))
                {
                    OnPointsUpdated(systemKey, collection);
                }
            }
        }

        // 触发点位变更事件
        private void OnPointsUpdated(string systemName, ObservableCollection<PointDataModel> points)
        {
            // 确保线程安全
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                GantryPointsChanged?.Invoke(this, new GantryPointsChangedEventArgs
                {
                    SystemName = systemName,
                    UpdatedPoints = points
                });

                // 同时触发属性变更通知
                //RaisePropertyChanged(nameof(GantryPoints));
            });
        }

        public void Dispose()
        {
            // 解除所有事件绑定
            _gantryPoints.CollectionChanged -= OnGantryPointsDictionaryChanged;

            foreach (var collection in _gantryPoints.Values)
            {
                collection.CollectionChanged -= OnGantryCollectionChanged;
            }
        }
        /// <summary>
        /// 龙门点位变更事件参数
        /// </summary>
        public class GantryPointsChangedEventArgs : EventArgs
        {
            /// <summary>
            /// 发生变更的龙门系统名称
            /// </summary>
            public string SystemName { get; set; }

            /// <summary>
            /// 变更后的点位集合
            /// </summary>
            public ObservableCollection<PointDataModel> UpdatedPoints { get; set; }

            /// <summary>
            /// 变更操作类型（添加/删除/替换等）
            /// </summary>
            public NotifyCollectionChangedAction ChangeAction { get; set; } = NotifyCollectionChangedAction.Reset;

            /// <summary>
            /// 新增的点位（如果变更类型是添加）
            /// </summary>
            public PointDataModel[] AddedPoints { get; set; }

            /// <summary>
            /// 移除的点位（如果变更类型是移除）
            /// </summary>
            public PointDataModel[] RemovedPoints { get; set; }

            /// <summary>
            /// 变更发生的索引位置
            /// </summary>
            public int ChangeStartingIndex { get; set; } = -1;
        }
        #endregion
    }
}
