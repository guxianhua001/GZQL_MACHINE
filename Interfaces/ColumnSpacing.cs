using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    public class ColumnSpacing : BindableBase
    {
        private int _columnIndex;
        private double _spacing;

        public int ColumnIndex
        {
            get => _columnIndex;
            set => SetProperty(ref _columnIndex, value);
        }

        public double Spacing
        {
            get => _spacing;
            set => SetProperty(ref _spacing, value);
        }
    }
}
