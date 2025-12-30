using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Interfaces
{
    public interface ITaskWithPoints
    {
        string TaskName { get; }
        ObservableCollection<PointViewModel> PinPoints { get; }
        // 物料二维码属性
        string MaterialQRCode { get; }

        // 添加所需的其他成员...
        void UpdatePointStatus(int index, bool? isOK);
        // ... 其他接口方法 ...
        event NotifyCollectionChangedEventHandler PointsChanged;
    }
}
