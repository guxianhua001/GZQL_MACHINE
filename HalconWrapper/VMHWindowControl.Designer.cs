
namespace HalconWrapper
{
    partial class VMHWindowControl
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VMHWindowControl));
            m_CtrlImageList = new System.Windows.Forms.ImageList(components);
            mCtrl_HWindow = new HalconDotNet.HWindowControl();
            m_CtrlHStatusLabelCtrl = new System.Windows.Forms.Label();
            lblMouseMessage = new System.Windows.Forms.Label();
            SuspendLayout();
            // 
            // m_CtrlImageList
            // 
            m_CtrlImageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
            m_CtrlImageList.ImageStream = (System.Windows.Forms.ImageListStreamer)resources.GetObject("m_CtrlImageList.ImageStream");
            m_CtrlImageList.TransparentColor = System.Drawing.Color.Transparent;
            m_CtrlImageList.Images.SetKeyName(0, "TableIcon.png");
            m_CtrlImageList.Images.SetKeyName(1, "PicturesIcon.png");
            // 
            // mCtrl_HWindow
            // 
            mCtrl_HWindow.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
            mCtrl_HWindow.BorderColor = System.Drawing.Color.FromArgb(64, 64, 64);
            mCtrl_HWindow.Dock = System.Windows.Forms.DockStyle.Fill;
            mCtrl_HWindow.ImagePart = new System.Drawing.Rectangle(0, 0, 128, 128);
            mCtrl_HWindow.Location = new System.Drawing.Point(10, 11);
            mCtrl_HWindow.Margin = new System.Windows.Forms.Padding(0);
            mCtrl_HWindow.Name = "mCtrl_HWindow";
            mCtrl_HWindow.Size = new System.Drawing.Size(798, 685);
            mCtrl_HWindow.TabIndex = 0;
            mCtrl_HWindow.WindowSize = new System.Drawing.Size(798, 685);
            mCtrl_HWindow.HMouseMove += HWindowControl_HMouseMove;
            mCtrl_HWindow.MouseLeave += mCtrl_HWindow_MouseLeave;
            // 
            // m_CtrlHStatusLabelCtrl
            // 
            m_CtrlHStatusLabelCtrl.AutoSize = true;
            m_CtrlHStatusLabelCtrl.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
            m_CtrlHStatusLabelCtrl.Dock = System.Windows.Forms.DockStyle.Bottom;
            m_CtrlHStatusLabelCtrl.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 134);
            m_CtrlHStatusLabelCtrl.ForeColor = System.Drawing.Color.White;
            m_CtrlHStatusLabelCtrl.Location = new System.Drawing.Point(10, 671);
            m_CtrlHStatusLabelCtrl.Margin = new System.Windows.Forms.Padding(5);
            m_CtrlHStatusLabelCtrl.Name = "m_CtrlHStatusLabelCtrl";
            m_CtrlHStatusLabelCtrl.Size = new System.Drawing.Size(0, 25);
            m_CtrlHStatusLabelCtrl.TabIndex = 1;
            // 
            // lblMouseMessage
            // 
            lblMouseMessage.BackColor = System.Drawing.Color.Transparent;
            lblMouseMessage.Dock = System.Windows.Forms.DockStyle.Bottom;
            lblMouseMessage.Font = new System.Drawing.Font("宋体", 10F);
            lblMouseMessage.Location = new System.Drawing.Point(10, 628);
            lblMouseMessage.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            lblMouseMessage.Name = "lblMouseMessage";
            lblMouseMessage.Size = new System.Drawing.Size(798, 43);
            lblMouseMessage.TabIndex = 2;
            lblMouseMessage.Text = "X:,Y:";
            lblMouseMessage.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // VMHWindowControl
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
            Controls.Add(lblMouseMessage);
            Controls.Add(m_CtrlHStatusLabelCtrl);
            Controls.Add(mCtrl_HWindow);
            DoubleBuffered = true;
            Margin = new System.Windows.Forms.Padding(10, 11, 10, 11);
            Name = "VMHWindowControl";
            Padding = new System.Windows.Forms.Padding(10, 11, 10, 11);
            Size = new System.Drawing.Size(818, 707);
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ImageList m_CtrlImageList;
        private System.Windows.Forms.Label m_CtrlHStatusLabelCtrl;
        public HalconDotNet.HWindowControl mCtrl_HWindow;
        private System.Windows.Forms.Label lblMouseMessage;
    }
}
