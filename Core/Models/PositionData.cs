// 在 PositionViewModel.cs 文件顶部添加这些数据模型

// JSON 数据模型
using Prism.Mvvm;

public class PositionData
{
    public int[] AxisIds { get; set; } = Array.Empty<int>();
    public Dictionary<string, PositionInfo> Positions { get; set; } = new Dictionary<string, PositionInfo>();
    public string RecipeName { get; set; } = string.Empty;
    public DateTime SavedTime { get; set; } = DateTime.UtcNow;
    public string Version { get; set; } = "1.0";
}

public class PositionInfo
{
    public double[] Coordinates { get; set; }
    public string Comment { get; set; }
}

// 界面显示模型
public class PositionDisplayItem : BindableBase
{
    private string _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private double[] _positions;
    public double[] Positions
    {
        get => _positions;
        set => SetProperty(ref _positions, value);
    }

    private string _comment;
    public string Comment
    {
        get => _comment;
        set => SetProperty(ref _comment, value);
    }
}