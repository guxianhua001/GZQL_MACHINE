using System.Collections.Generic;

namespace Core.Models
{
    public enum ZScanDataFormat
    {
        Double,
        DoubleArray
    }

    public class ZScanGlobalVariableLink
    {
        private bool _isLinked;
        private string _variableName = string.Empty;
        private GlobalVariableType _variableType = GlobalVariableType.Double;

        public bool IsLinked
        {
            get => _isLinked;
            set => _isLinked = value;
        }

        public string VariableName
        {
            get => _variableName;
            set => _variableName = value ?? string.Empty;
        }

        public GlobalVariableType VariableType
        {
            get => _variableType;
            set => _variableType = value;
        }
    }

    public class ZScanTableConfig
    {
        private string _tableName = string.Empty;
        private ZScanDataFormat _dataFormat = ZScanDataFormat.Double;
        private ZScanGlobalVariableLink _zActualLink;
        private List<ZScanPointData> _points = new List<ZScanPointData>();
        private ZScanCalibrationConfig _calibration = new ZScanCalibrationConfig();

        public string TableName
        {
            get => _tableName;
            set => _tableName = value ?? string.Empty;
        }

        public ZScanDataFormat DataFormat
        {
            get => _dataFormat;
            set => _dataFormat = value;
        }

        public ZScanGlobalVariableLink ZActualLink
        {
            get => _zActualLink;
            set => _zActualLink = value;
        }

        public List<ZScanPointData> Points
        {
            get => _points;
            set => _points = value ?? new List<ZScanPointData>();
        }

        public ZScanCalibrationConfig Calibration
        {
            get => _calibration;
            set => _calibration = value ?? new ZScanCalibrationConfig();
        }
    }

    public class ZScanPointData
    {
        public int Segment { get; set; }
        public int PointNumber { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double ZNominal { get; set; }
        public double ZMeasured { get; set; }
        public double DeltaZ { get; set; }
        public string Description { get; set; } = string.Empty;
        public double Nominal { get; set; }
        public double Range { get; set; }
        public int DataIndex { get; set; }
        public string Status { get; set; } = "Pending";
        public ZScanDataFormat PointType { get; set; } = ZScanDataFormat.Double;
        public ZScanGlobalVariableLink GlobalVariableLink { get; set; }
    }

    /// <summary>
    /// Z-SCAN 配置文件模型，支持双针头（Dz1/Dz2）各自独立的表格集合
    /// </summary>
    public class ZScanConfigFile
    {
        /// <summary> 针头1（Dz1）的表格集合 </summary>
        public List<ZScanTableConfig> Needle1Tables { get; set; } = new List<ZScanTableConfig>();
        /// <summary> 针头2（Dz2）的表格集合 </summary>
        public List<ZScanTableConfig> Needle2Tables { get; set; } = new List<ZScanTableConfig>();
        /// <summary> 向后兼容旧格式（单针头时代的表格集合） </summary>
        public List<ZScanTableConfig> Tables { get; set; }
        public string DefaultTableName { get; set; } = string.Empty;
    }
}
