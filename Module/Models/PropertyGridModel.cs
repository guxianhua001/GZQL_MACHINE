using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Framework.Models
{
    public class PropertyGridDemoModel 
    {
        [Category("类别1")]
        [DisplayName("上料移栽X轴运动速度(mm/s)"), Description("运动速度(mm/s)")]
        public string String { get; set; }

        [Category("类别2")]
        [DisplayName("整型")]
        public int Integer { get; set; }

        [Category("类别3")]
        [DisplayName("布尔型")]
        public bool Boolean { get; set; }

        [Category("类别1")]
        [DisplayName("枚举型")]
        public Gender Enum { get; set; }
        [DisplayName("枚举型")]
        public HorizontalAlignment HorizontalAlignment { get; set; }
        [DisplayName("枚举型")]
        public VerticalAlignment VerticalAlignment { get; set; }
        [DisplayName("图像类型")]
        public ImageSource ImageSource { get; set; }
    }

    public enum Gender
    {
        [Description("男性")] //可考虑自定义编辑器，3.2不支持
        Male,
        [Description("女性")] //可考虑自定义编辑器，3.2不支持
        Female
    }

}
