using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    public class GridPoint : BindableBase
    {
        private bool _isVisited;
        public double X { get; set; }
        public double Y { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }

        public bool IsVisited
        {
            get => _isVisited;
            set => SetProperty(ref _isVisited, value);
        }
    }
}
