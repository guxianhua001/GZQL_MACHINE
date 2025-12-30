using Core.Abstraction;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;

namespace Stations.TaskParameters
{
    /// <summary>
    /// 点胶扩展配方参数
    /// </summary>
    [DataContract]
    public class DispenserExtendedParameters : TaskParametersBase
    {
        public override string Identifier => "点胶扩展参数";

        public DispenserExtendedParameters()
        {
            // 初始化Tab高度参数
            for (int i = 0; i < TabHeights.Length; i++)
            {
                TabHeights[i] = new HeightLimitParameters();
            }

            // 初始化点胶基准位参数
            for (int i = 0; i < DispensingPositions.Length; i++)
            {
                DispensingPositions[i] = new PositionLimitParameters();
            }
        }

        #region Tab高度参数 (Tab1-6)

        [Category("Tab高度参数")]
        [DisplayName("Tab1高度")]
        [Description("Tab1基准高度和限位")]
        [DataMember]
        public HeightLimitParameters Tab1Height { get => TabHeights[0]; set => TabHeights[0] = value; }

        [Category("Tab高度参数")]
        [DisplayName("Tab2高度")]
        [Description("Tab2基准高度和限位")]
        [DataMember]
        public HeightLimitParameters Tab2Height { get => TabHeights[1]; set => TabHeights[1] = value; }

        [Category("Tab高度参数")]
        [DisplayName("Tab3高度")]
        [Description("Tab3基准高度和限位")]
        [DataMember]
        public HeightLimitParameters Tab3Height { get => TabHeights[2]; set => TabHeights[2] = value; }

        [Category("Tab高度参数")]
        [DisplayName("Tab4高度")]
        [Description("Tab4基准高度和限位")]
        [DataMember]
        public HeightLimitParameters Tab4Height { get => TabHeights[3]; set => TabHeights[3] = value; }

        [Category("Tab高度参数")]
        [DisplayName("Tab5高度")]
        [Description("Tab5基准高度和限位")]
        [DataMember]
        public HeightLimitParameters Tab5Height { get => TabHeights[4]; set => TabHeights[4] = value; }

        [Category("Tab高度参数")]
        [DisplayName("Tab6高度")]
        [Description("Tab6基准高度和限位")]
        [DataMember]
        public HeightLimitParameters Tab6Height { get => TabHeights[5]; set => TabHeights[5] = value; }

        [Browsable(false)]
        [DataMember]
        public HeightLimitParameters[] TabHeights { get; set; } = new HeightLimitParameters[6];

        #endregion

        #region 点胶基准位参数 (1-12)

        [Category("点胶基准位")]
        [DisplayName("基准位1")]
        [Description("点胶基准位1参数")]
        [DataMember]
        public PositionLimitParameters DispensingPosition1 { get => DispensingPositions[0]; set => DispensingPositions[0] = value; }

        [Category("点胶基准位")]
        [DisplayName("基准位2")]
        [Description("点胶基准位2参数")]
        [DataMember]
        public PositionLimitParameters DispensingPosition2 { get => DispensingPositions[1]; set => DispensingPositions[1] = value; }

        [Category("点胶基准位")]
        [DisplayName("基准位3")]
        [Description("点胶基准位3参数")]
        [DataMember]
        public PositionLimitParameters DispensingPosition3 { get => DispensingPositions[2]; set => DispensingPositions[2] = value; }

        [Category("点胶基准位")]
        [DisplayName("基准位4")]
        [Description("点胶基准位4参数")]
        [DataMember]
        public PositionLimitParameters DispensingPosition4 { get => DispensingPositions[3]; set => DispensingPositions[3] = value; }

        [Category("点胶基准位")]
        [DisplayName("基准位5")]
        [Description("点胶基准位5参数")]
        [DataMember]
        public PositionLimitParameters DispensingPosition5 { get => DispensingPositions[4]; set => DispensingPositions[4] = value; }

        [Category("点胶基准位")]
        [DisplayName("基准位6")]
        [Description("点胶基准位6参数")]
        [DataMember]
        public PositionLimitParameters DispensingPosition6 { get => DispensingPositions[5]; set => DispensingPositions[5] = value; }

        [Category("点胶基准位")]
        [DisplayName("基准位7")]
        [Description("点胶基准位7参数")]
        [DataMember]
        public PositionLimitParameters DispensingPosition7 { get => DispensingPositions[6]; set => DispensingPositions[6] = value; }

        [Category("点胶基准位")]
        [DisplayName("基准位8")]
        [Description("点胶基准位8参数")]
        [DataMember]
        public PositionLimitParameters DispensingPosition8 { get => DispensingPositions[7]; set => DispensingPositions[7] = value; }

        [Category("点胶基准位")]
        [DisplayName("基准位9")]
        [Description("点胶基准位9参数")]
        [DataMember]
        public PositionLimitParameters DispensingPosition9 { get => DispensingPositions[8]; set => DispensingPositions[8] = value; }

        [Category("点胶基准位")]
        [DisplayName("基准位10")]
        [Description("点胶基准位10参数")]
        [DataMember]
        public PositionLimitParameters DispensingPosition10 { get => DispensingPositions[9]; set => DispensingPositions[9] = value; }

        [Category("点胶基准位")]
        [DisplayName("基准位11")]
        [Description("点胶基准位11参数")]
        [DataMember]
        public PositionLimitParameters DispensingPosition11 { get => DispensingPositions[10]; set => DispensingPositions[10] = value; }

        [Category("点胶基准位")]
        [DisplayName("基准位12")]
        [Description("点胶基准位12参数")]
        [DataMember]
        public PositionLimitParameters DispensingPosition12 { get => DispensingPositions[11]; set => DispensingPositions[11] = value; }

        [Browsable(false)]
        [DataMember]
        public PositionLimitParameters[] DispensingPositions { get; set; } = new PositionLimitParameters[12];

        #endregion

        #region 系统参数

        [Category("系统参数")]
        [DisplayName("安全高度")]
        [Description("Z轴安全高度")]
        [DataMember]
        public double SafeHeight { get; set; } = 50.0;

        [Category("系统参数")]
        [DisplayName("点胶高度")]
        [Description("点胶工作高度")]
        [DataMember]
        public double DispensingHeight { get; set; } = 5.0;

        [Category("系统参数")]
        [DisplayName("Z轴速度")]
        [Description("Z轴移动速度")]
        [DataMember]
        public double ZAxisSpeed { get; set; } = 20.0;

        [Category("系统参数")]
        [DisplayName("XY轴速度")]
        [Description("XY轴移动速度")]
        [DataMember]
        public double XYAxisSpeed { get; set; } = 100.0;

        #endregion
    }

    /// <summary>
    /// 高度限位参数
    /// </summary>
    [DataContract]
    public class HeightLimitParameters : BindableBase
    {
        private double _baseline;
        private double _upperLimit;
        private double _lowerLimit;
        private bool _isCalibrated;
        private double _compensation;

        [Category("基准参数")]
        [DisplayName("基准高度")]
        [Description("基准高度值(mm)")]
        [DataMember]
        public double Baseline
        {
            get => _baseline;
            set => SetProperty(ref _baseline, value);
        }

        [Category("限位参数")]
        [DisplayName("上限")]
        [Description("高度上限(mm)")]
        [DataMember]
        public double UpperLimit
        {
            get => _upperLimit;
            set => SetProperty(ref _upperLimit, value);
        }

        [Category("限位参数")]
        [DisplayName("下限")]
        [Description("高度下限(mm)")]
        [DataMember]
        public double LowerLimit
        {
            get => _lowerLimit;
            set => SetProperty(ref _lowerLimit, value);
        }

        [Category("校准参数")]
        [DisplayName("已校准")]
        [Description("是否已完成校准")]
        [DataMember]
        public bool IsCalibrated
        {
            get => _isCalibrated;
            set => SetProperty(ref _isCalibrated, value);
        }

        [Category("校准参数")]
        [DisplayName("补偿值")]
        [Description("高度补偿值(mm)")]
        [DataMember]
        public double Compensation
        {
            get => _compensation;
            set => SetProperty(ref _compensation, value);
        }
    }

    /// <summary>
    /// 位置限位参数
    /// </summary>
    [DataContract]
    public class PositionLimitParameters : BindableBase
    {
        private double _baselineX;
        private double _baselineY;
        private double _upperLimitX;
        private double _lowerLimitX;
        private double _upperLimitY;
        private double _lowerLimitY;
        private bool _isCalibrated;
        private double _compensationX;
        private double _compensationY;

        [Category("基准参数")]
        [DisplayName("X基准")]
        [Description("X轴基准位置(mm)")]
        [DataMember]
        public double BaselineX
        {
            get => _baselineX;
            set => SetProperty(ref _baselineX, value);
        }

        [Category("基准参数")]
        [DisplayName("Y基准")]
        [Description("Y轴基准位置(mm)")]
        [DataMember]
        public double BaselineY
        {
            get => _baselineY;
            set => SetProperty(ref _baselineY, value);
        }

        [Category("限位参数")]
        [DisplayName("X上限")]
        [Description("X轴位置上限(mm)")]
        [DataMember]
        public double UpperLimitX
        {
            get => _upperLimitX;
            set => SetProperty(ref _upperLimitX, value);
        }

        [Category("限位参数")]
        [DisplayName("X下限")]
        [Description("X轴位置下限(mm)")]
        [DataMember]
        public double LowerLimitX
        {
            get => _lowerLimitX;
            set => SetProperty(ref _lowerLimitX, value);
        }

        [Category("限位参数")]
        [DisplayName("Y上限")]
        [Description("Y轴位置上限(mm)")]
        [DataMember]
        public double UpperLimitY
        {
            get => _upperLimitY;
            set => SetProperty(ref _upperLimitY, value);
        }

        [Category("限位参数")]
        [DisplayName("Y下限")]
        [Description("Y轴位置下限(mm)")]
        [DataMember]
        public double LowerLimitY
        {
            get => _lowerLimitY;
            set => SetProperty(ref _lowerLimitY, value);
        }

        [Category("校准参数")]
        [DisplayName("已校准")]
        [Description("是否已完成校准")]
        [DataMember]
        public bool IsCalibrated
        {
            get => _isCalibrated;
            set => SetProperty(ref _isCalibrated, value);
        }

        [Category("校准参数")]
        [DisplayName("X补偿")]
        [Description("X轴补偿值(mm)")]
        [DataMember]
        public double CompensationX
        {
            get => _compensationX;
            set => SetProperty(ref _compensationX, value);
        }

        [Category("校准参数")]
        [DisplayName("Y补偿")]
        [Description("Y轴补偿值(mm)")]
        [DataMember]
        public double CompensationY
        {
            get => _compensationY;
            set => SetProperty(ref _compensationY, value);
        }
    }
}
