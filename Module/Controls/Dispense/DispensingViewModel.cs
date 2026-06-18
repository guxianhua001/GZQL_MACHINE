using Prism.Mvvm;

namespace Module.ViewModels
{
    /// <summary>
    /// 点胶工站视图模型——工站级协调与状态管理
    /// </summary>
    public class DispensingViewModel : BindableBase
    {
        private int _selectedTabIndex;
        /// <summary>
        /// 当前选中的选项卡索引
        /// 关键：必须初始化为 0 并绑定到 TabControl.SelectedIndex，
        /// 以避免 MaterialDesign Underline 在布局未完成时（ActualWidth=NaN）触发动画导致导航失败
        /// </summary>
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public DispensingViewModel()
        {
            // 初始化为第一个 Tab，确保 TabControl 加载时 Underline 控件有确定的状态
            _selectedTabIndex = 0;
        }
    }
}
