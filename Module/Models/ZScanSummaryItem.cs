using Core.Models;
using Prism.Mvvm;

namespace Module.Models
{
    /// <summary>
    /// 汇总表格行数据
    /// </summary>
    public class ZScanSummaryItem : BindableBase
    {
        private string _assyGroup;
        private string _siteId;
        private string _subAssy;
        private int _points;
        private double _zNominal;
        private double _zMaxDelta;
        private ScanStatus _status;

        public string AssyGroup { get => _assyGroup; set => SetProperty(ref _assyGroup, value); }
        public string SiteId { get => _siteId; set => SetProperty(ref _siteId, value); }
        public string SubAssy { get => _subAssy; set => SetProperty(ref _subAssy, value); }
        public int Points { get => _points; set => SetProperty(ref _points, value); }
        public double ZNominal { get => _zNominal; set => SetProperty(ref _zNominal, value); }
        public double ZMaxDelta { get => _zMaxDelta; set => SetProperty(ref _zMaxDelta, value); }
        public ScanStatus Status { get => _status; set => SetProperty(ref _status, value); }
    }

    /// <summary>
    /// 逐点测量数据
    /// 支持配置化测量点定义和实时检测结果展示
    /// </summary>
    public class ZScanPointDetail : BindableBase
    {
        #region 原有字段（保持向后兼容）

        private int _segment;
        private int _pointNumber;
        private double _x;
        private double _y;
        private double _zNominal;
        private double _zMeasured;
        private double _deltaZ;
        private string _featureName;

        /// <summary>
        /// 测量段号（分区标识）
        /// </summary>
        public int Segment { get => _segment; set => SetProperty(ref _segment, value); }

        /// <summary>
        /// 测量点序号（段内编号）
        /// </summary>
        public int PointNumber { get => _pointNumber; set => SetProperty(ref _pointNumber, value); }

        /// <summary>
        /// X坐标值（理论/实际位置）
        /// </summary>
        public double X { get => _x; set => SetProperty(ref _x, value); }

        /// <summary>
        /// Y坐标值（理论/实际位置）
        /// </summary>
        public double Y { get => _y; set => SetProperty(ref _y, value); }

        /// <summary>
        /// Z标称值（目标值）
        /// 与 Nominal 属性保持同步，提供双重访问接口
        /// </summary>
        public double ZNominal
        {
            get => _zNominal;
            set
            {
                if (SetProperty(ref _zNominal, value))
                {
                    _nominal = value; // 同步更新 Nominal 字段
                    RaisePropertyChanged(nameof(Nominal));
                }
            }
        }

        /// <summary>
        /// Z实测值（3D相机采集的实际高度）
        /// </summary>
        public double ZMeasured { get => _zMeasured; set => SetProperty(ref _zMeasured, value); }

        /// <summary>
        /// 偏差值 = 实测值 - 标称值
        /// </summary>
        public double DeltaZ { get => _deltaZ; set => SetProperty(ref _deltaZ, value); }

        /// <summary>
        /// 特征名称（如 tab001, pillar001）
        /// 向后兼容保留，建议使用 Description 替代
        /// </summary>
        public string FeatureName { get => _featureName; set => SetProperty(ref _featureName, value); }

        #endregion

        #region 新增字段（支持 ZScanDetailView 功能扩展）

        private string _description = string.Empty;
        private double _nominal;
        private double _range;
        private int _dataIndex;
        private string _status = "Pending";
        private ZScanDataFormat _pointType = ZScanDataFormat.Double;
        private ZScanGlobalVariableLink _globalVariableLink;

        /// <summary>
        /// 自由输入的描述信息
        /// 替代原有 FeatureName 的语义，支持用户自定义测量点说明
        /// 用途：在界面上显示友好的点位描述，如"左侧定位柱顶部"、"焊缝中心点"等
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                if (SetProperty(ref _description, value))
                {
                    // 可选：同步更新 FeatureName 以保持兼容性
                    // _featureName = value;
                    // OnPropertyChanged(nameof(FeatureName));
                }
            }
        }

        /// <summary>
        /// 标称值（目标值/理论值）
        /// 与 ZNominal 保持语义一致，提供更通用的命名
        /// 用于：配置界面设置、公差计算基准、报表输出
        /// </summary>
        public double Nominal
        {
            get => _nominal;
            set
            {
                if (SetProperty(ref _nominal, value))
                {
                    // 同步更新 ZNominal 以保持兼容性
                    _zNominal = value;
                    RaisePropertyChanged(nameof(ZNominal));
                }
            }
        }

        /// <summary>
        /// 允许偏差范围（公差带半宽）
        /// 用于判定 Pass/Fail：|DeltaZ| <= Range 则为 Pass
        /// 示例：Range=0.1 表示允许 ±0.1mm 的偏差
        /// </summary>
        public double Range { get => _range; set => SetProperty(ref _range, value); }

        /// <summary>
        /// 3D相机返回数据中的序号位置（0-based 或 1-based）
        /// 用于配置化数据接收：指定该测量点对应相机数据数组的哪个索引
        /// 示例：DataIndex=0 表示取相机数据的第1个点
        /// </summary>
        public int DataIndex { get => _dataIndex; set => SetProperty(ref _dataIndex, value); }

        /// <summary>
        /// 状态判定结果
        /// 可选值：
        /// - "Pending": 待检测（初始状态）
        /// - "Pass": 合格（偏差在允许范围内）
        /// - "Fail": 不合格（超出允许范围）
        /// 用于：UI 状态显示、统计汇总、报表生成
        /// </summary>
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public ZScanDataFormat PointType
        {
            get => _pointType;
            set => SetProperty(ref _pointType, value);
        }

        public ZScanGlobalVariableLink GlobalVariableLink
        {
            get => _globalVariableLink;
            set
            {
                if (SetProperty(ref _globalVariableLink, value))
                {
                    RaisePropertyChanged(nameof(IsGlobalVarLinked));
                }
            }
        }

        public bool IsGlobalVarLinked => _globalVariableLink?.IsLinked == true;

        private string _linkedGlobalVarName;
        public string LinkedGlobalVarName
        {
            get => _linkedGlobalVarName;
            set
            {
                if (SetProperty(ref _linkedGlobalVarName, value))
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        GlobalVariableLink = new ZScanGlobalVariableLink
                        {
                            IsLinked = true,
                            VariableName = value,
                            VariableType = PointType == ZScanDataFormat.DoubleArray
                                ? GlobalVariableType.DoubleArray
                                : GlobalVariableType.Double
                        };
                    }
                    else
                    {
                        GlobalVariableLink = null;
                    }
                    RaisePropertyChanged(nameof(IsGlobalVarLinked));
                }
            }
        }

        #endregion

        #region 全局变量链接属性

        private ZScanGlobalVariableLink _zActualLink;

        /// <summary>
        /// Z实测值全局变量链接配置
        /// 用于将3D相机测量值链接到全局变量，支持数据回写
        /// </summary>
        public ZScanGlobalVariableLink ZActualLink
        {
            get => _zActualLink;
            set
            {
                if (SetProperty(ref _zActualLink, value))
                {
                    RaisePropertyChanged(nameof(IsZActualLinked));
                }
            }
        }

        /// <summary>
        /// Z实测值是否已链接全局变量
        /// 用于UI显示链接图标
        /// </summary>
        public bool IsZActualLinked => _zActualLink?.IsLinked == true;

        #endregion

        /// <summary>
        /// 构造函数：初始化默认值并建立字段映射关系
        /// </summary>
        public ZScanPointDetail()
        {
            _status = "Pending";
            _description = string.Empty;
        }

        /// <summary>
        /// 手动触发属性变更通知（供外部类调用以刷新 UI）
        /// </summary>
        public void NotifyPropertyChanged(string propertyName)
        {
            RaisePropertyChanged(propertyName);
        }
    }

    /// <summary>
    /// 扫描状态枚举
    /// </summary>
    public enum ScanStatus
    {
        NotScanned,
        ScannedOk,
        HighDelta,
        Failed
    }
}