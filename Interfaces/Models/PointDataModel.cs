using System.ComponentModel;

namespace Interfaces.Models
{
    public class PointDataModel : INotifyPropertyChanged
    {
        private double _upperX;
        public double UpperX
        {
            get => _upperX;
            set { _upperX = value; OnPropertyChanged(); }
        }

        private double _upperY;
        public double UpperY
        {
            get => _upperY;
            set { _upperY = value; OnPropertyChanged(); }
        }

        private double _lowerX;
        public double LowerX
        {
            get => _lowerX;
            set { _lowerX = value; OnPropertyChanged(); }
        }

        private double _lowerY;
        public double LowerY
        {
            get => _lowerY;
            set { _lowerY = value; OnPropertyChanged(); }
        }

        private string _name = "未命名点位";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // 深拷贝方法
        public PointDataModel Clone()
        {
            return new PointDataModel
            {
                Name = this.Name,
                UpperX = this.UpperX,
                UpperY = this.UpperY,
                LowerX = this.LowerX,
                LowerY = this.LowerY
            };
        }
    }
}

