
using Newtonsoft.Json;

public class CalibrationPoint
{
    public int Index { get; set; }
    public double MachineX { get; set; }
    public double MachineY { get; set; }
    public double PixelX { get; set; }
    public double PixelY { get; set; }
    public string PointType { get; set; } = "网格点";
    public string Status { get; set; } = "待标定";
    public string CameraType { get; set; } = "Side"; // Side, Bottom
    public bool Is9Point { get; set; } = true;

    // 用于UI显示的颜色
    [JsonIgnore]
    public string StatusColor => Status switch
    {
        "已标定" => "Green",
        "待标定" => "Gray",
        "标定中" => "Orange",
        _ => "Gray"
    };
}