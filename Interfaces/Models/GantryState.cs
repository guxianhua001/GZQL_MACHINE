using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

public enum GantryType { Upper, Lower }
public class GantryState
{
    public PointF UpperPosition { get; set; }
    public PointF LowerPosition { get; set; }
    public double SyncError { get; set; }
    public double UpperSyncError { get; set; }
    public double LowerSyncError { get; set; }

    public string SyncStatusUpper { get; set; } = "同步正常";
    public string SyncStatusLower { get; set; } = "同步正常";

    public SolidColorBrush SyncColorUpper { get; set; } = System.Windows.Media.Brushes.Green;
    public SolidColorBrush SyncColorLower { get; set; } = System.Windows.Media.Brushes.Green;
}
