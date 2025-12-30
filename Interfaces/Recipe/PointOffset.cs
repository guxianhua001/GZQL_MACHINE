using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    [Serializable]
    // PointOffset模型
    public class PointOffset : BindableBase
    {
        private double _xOffset;
        private double _yOffset;

        public double XOffset
        {
            get => _xOffset;
            set => SetProperty(ref _xOffset, value);
        }

        public double YOffset
        {
            get => _yOffset;
            set => SetProperty(ref _yOffset, value);
        }

        // 需要无参构造函数以支持序列化
        public PointOffset() { }

    }
}
