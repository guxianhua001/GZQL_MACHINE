using System.Windows.Controls;

namespace Module.Views
{
    /// <summary>
    /// CAD 点编辑器视图（薄包装）
    /// 内嵌 CadPointEditorControl，所有功能已迁移至控件内部
    /// </summary>
    public partial class CadPointEditorView : UserControl
    {
        // 构造函数：初始化组件
        public CadPointEditorView()
        {
            InitializeComponent();
        }
    }
}
