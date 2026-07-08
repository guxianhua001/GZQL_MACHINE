using System.Windows;
using System.Windows.Data;

namespace Module.Common.Converters
{
    /// <summary>
    /// 数据绑定代理——突破 DataGrid 列(DataGridColumn)不在可视化树中、无法继承 DataContext 的限制。
    /// 用法：在 DataGrid.Resources 中声明 &lt;converters:BindingProxy x:Key="VmProxy" Data="{Binding}"/&gt;，
    /// 随后列的 Visibility 可绑定到 {Binding Data.IsXxx, Source={StaticResource VmProxy}}。
    /// 继承 Freezable 以使绑定在资源字典中正常生效。
    /// </summary>
    public class BindingProxy : Freezable
    {
        /// <summary>代理承载的数据对象（通常绑定到外层 DataContext）</summary>
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));

        /// <summary>代理承载的数据对象</summary>
        public object Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        protected override Freezable CreateInstanceCore() => new BindingProxy();
    }
}
