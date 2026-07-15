using System;
using HalconWrapper;
using System.Collections;
using HalconDotNet;
using HalconWrapper.Config;
using System.Collections.Generic;
using System.Diagnostics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.AxHost;

namespace HalconWrapper.Model
{
	public delegate void IconicDelegate(int val);
	public delegate void FuncDelegate();

    /// <summary>
    /// This class works as a wrapper class for the HALCON window
    /// HWindow. HWndCtrl is in charge of the visualization.
    /// You can move and zoom the visible image part by using GUI component 
    /// inputs or with the mouse. The class HWndCtrl uses a graphics stack 
    /// to manage the iconic objects for the display. Each object is linked 
    /// to a graphical context, which determines how the object is to be drawn.
    /// The context can be changed by calling changeGraphicSettings().
    /// The graphical "modes" are defined by the class GraphicsContext and 
    /// map most of the dev_set_* operators provided in HDevelop.
    /// </summary>
    public class HWndCtrl
    {
        /// <summary>��ʾROI</summary>
        public const int MODE_INCLUDE_ROI = 1;
        /// <summary>����ʾROI</summary>
		public const int MODE_EXCLUDE_ROI = 2;
        /// <summary>������¼���ִ���κβ���</summary>
        public const int MODE_VIEW_NONE = 10;
        /// <summary>����������¼���ִ��</summary>
        public const int MODE_VIEW_ZOOM = 11;
        /// <summary>������¼���ִ���ƶ�</summary>
        public const int MODE_VIEW_MOVE = 12;
        /// <summary>������¼����зŴ�</summary>
        public const int MODE_VIEW_ZOOMWINDOW = 13;
        /// <summary>����</summary>
        public const int MODE_ERASER = 14;
        /// <summary>��Ϳ</summary>
        public const int MODE_PAINT = 15;
        /// <summary>����ROIģʽ�ĳ���:ROI�����š�</summary>
        public const int MODE_ROI_POS = 21;
        /// <summary>����ROIģʽ�ĳ���:����ROI���š�</summary>
        public const int MODE_ROI_NEG = 22;
        /// <summary>����ROIģʽ�ĳ���:û��ģ���������Ϊ/// ����ROI������ܺ�.</summary>
        public const int MODE_ROI_NONE = 23;
        /// <summary>����Ϊ��ͼ�񷢳��źŵ�ί����Ϣ</summary>
        public const int EVENT_UPDATE_IMAGE = 31;
        /// <summary>�����������ļ��ж�ȡͼ��ʱ���������źŵ�ί����Ϣ </summary>
        public const int ERR_READING_IMG = 32;
        /// <summary>�����ڶ���ͼ��������ʱ����ί����Ϣ����Ϊ�źŴ���</summary>
        public const int ERR_DEFINING_GC = 33;
        /// <summary>����ģ��������µĳ���</summary>
        public const int EVENT_UPDATE_ROI = 50;
        public const int EVENT_CHANGED_ROI_SIGN = 51;
        /// <summary>����ģ��������µĳ���</summary>
        public const int EVENT_MOVING_ROI = 52;
        public const int EVENT_DELETED_ACTROI = 53;
        public const int EVENT_DELETED_ALL_ROIS = 54;
        public const int EVENT_ACTIVATED_ROI = 55;
        public const int EVENT_CREATED_ROI = 56;
        /// <summary> ���Է�����ͼ���ϵ�HALCON������������,��ջ����ʧ������ÿ������Ķ��󣬵�һ����Ŀ,���ٴδ�ջ���Ƴ���</summary>
        private const int MAXNUMOBJLIST = 2;//ԭʼֵΪ50 ʵ����2������,������ֻ�Ǵ洢����ͼƬ
        public Action PaintCrossEvent;
        private int ViewState;
        private bool MousePressed = false;
        private double StartX, StartY;
        /// <summary>HALCON window</summary>
        private HWindowControl ViewPort;
        /// <summary>ROIController��ʵ����������ROI����</summary>
        private ROIController ROIManager;
        /// <summary>�������̺��Ƿ���Ӧ����¼� </summary>
        private int DispROI;
        /// <summary>�����¼�����</summary>
        public bool drawModel = false;
        // ��ֹ/�������ͼƬ�ƶ�
        public bool isMoveEnable = false;
        private int imageWidth = 0;
        private int imageHeight = 0;
        public int ImageWidth
        {
            get
            {
                return imageWidth;
            }
            set
            {
                imageWidth = value;
            }
        }

        public int ImageHeight
        {
            get
            {
                return imageHeight;
            }
            set
            {
                imageHeight = value;
            }
        }
        #region �����������細�ڳߴ����ʾͼ�񲿷�
        private int WindowWidth, WindowHeight, PrevCompX, PrevCompY;
        private int[] CompRangeX;
        private int[] CompRangeY;
        /// <summary> ͼ�����꣬��������HALCON��������ʾ��ͼ�񲿷� </summary>
        private double ImgRow1, ImgCol1, ImgRow2, ImgCol2, StepSizeX, StepSizeY;
        /// <summary>�׳��쳣ʱ�Ĵ�����Ϣ</summary>
        public string ExceptionText = "";
        /// <summary>ί�з���֪ͨ��Ϣ�������� </summary>
        public FuncDelegate AddInfoDelegate;
        /// <summary>֪ͨHWndCtrlʵ����ʧ������ </summary>
        public IconicDelegate NotifyIconObserver;
        private HWindow ZoomWindow;
        /// <summary> ���Ų���</summary>
        private double ZoomWndFactor, ZoomAddOn;
        private int ZoomWndSize;
        /// <summary> ���Ƶ�HALCON���ڵ�HALCON�����б� </summary>
        //private ArrayList HObjImageList;
        /// <summary>��������ͼ�������ĵ�ʵ���� HALCON���ڡ�����ͼ������</summary>
        private GraphicsContext mGC;
         public event EventHandler<ShowMessageEventArgs> ShowMessageEvent;
        private void TriggerShowMessageEvent(ShowMessageEventArgs e)
        {
            if (ShowMessageEvent != null)
            {
                ShowMessageEvent(this, e);
            }
        }
        #region ͿĨ����
        /// <summary>�Ҷ�ֵ������λ��</summary>
        string message;
        HRegion BrushRegion;
        private int StateView;
        public List<HObjectEntry> HObjList;
        #endregion
        #endregion
        /// <summary>��ʼ��ͼ��ߴ硢���ί�к�ʵ����ͼ�����������á� </summary>
        protected internal HWndCtrl(HWindowControl view)
        {
            ViewPort = view;
            ViewState = MODE_VIEW_NONE;
            WindowWidth = ViewPort.Size.Width;
            WindowHeight = ViewPort.Size.Height;
            ZoomWndFactor = (double)ImageWidth / ViewPort.Width;
            ZoomAddOn = Math.Pow(0.9, 5);
            ZoomWndSize = 150;
            /*default*/
            CompRangeX = new int[] { 0, 100 };
            CompRangeY = new int[] { 0, 100 };
            PrevCompX = PrevCompY = 0;
            DispROI = MODE_INCLUDE_ROI;//1;
            ViewPort.HMouseUp += new HMouseEventHandler(this.MouseUp);
            ViewPort.HMouseDown += new HMouseEventHandler(this.MouseDown);
            ViewPort.HMouseWheel += new HMouseEventHandler(this.MouseWheel);
            ViewPort.HMouseMove += new HMouseEventHandler(this.MouseMoved);
            AddInfoDelegate = new FuncDelegate(dummyV);
            NotifyIconObserver = new IconicDelegate(dummy);
            // graphical stack 
            HObjList = new List<HObjectEntry>();
            //HObjImageList = new ArrayList(20);
            mGC = new GraphicsContext();
            mGC.gcNotification = new GCDelegate(ExceptionGC);
        }

        private void MouseWheel(object sender, HMouseEventArgs e)
        {
            //�ر������¼�
            if (drawModel)
            {
                return;
            }
            double scale;
            if (e.Delta > 0)
                scale = 0.9;
            else
                scale = 1 / 0.9;
            ZoomImage(e.X, e.Y, scale);
            PaintCrossEvent?.Invoke();
        }
        /// <summary>��ȡͼ��ĳߴ��Ե����Լ��Ĵ������� </summary>
        private void SetImagePart(HImage image)
        {
            string s;
            int w, h;

            image.GetImagePointer1(out s, out w, out h);
            SetImagePart(0, 0, h, w);
        }
        /// <summary>��������ṩ��ֵ�����������ã����ϽǺ����½� </summary>
        private void SetImagePart(int r1, int c1, int r2, int c2)
        {
            ImgRow1 = r1;
            ImgCol1 = c1;
            ImgRow2 = ImageHeight = r2;
            ImgCol2 = ImageWidth = c2;

            System.Drawing.Rectangle rect = ViewPort.ImagePart;
            rect.X = (int)ImgCol1;
            rect.Y = (int)ImgRow1;
            rect.Height = (int)ImageHeight;
            rect.Width = (int)ImageWidth;
            ViewPort.ImagePart = rect;
        }
        /// <summary>����HALCON�����е�����¼�����ͼģʽ(���ţ��ƶ����Ŵ����)�� </summary>
        public void SetViewState(int mode)
        {
            ViewState = mode;
            if (ROIManager != null)
                ROIManager.ResetROI();
        }
        /********************************************************************/
        private void dummy(int val)
        {
        }
        private void dummyV()
        {
        }
        /*******************************************************************/
        private void ExceptionGC(string message)
        {
            ExceptionText = message;
            NotifyIconObserver(ERR_DEFINING_GC);
        }

        /// <summary>
        /// Paint or don't paint the ROIs into the HALCON window by 
        /// defining the parameter to be equal to 1 or not equal to 1.
        /// </summary>
        public void SetDispLevel(int mode)
        {
            DispROI = mode;
        }

        /****************************************************************************/
        /*                          graphical element                               */
        /****************************************************************************/
        /// <summary>
        /// 从 HalconWindow 读取实际视口并同步内部字段
        /// FitToAll 等外部调用使用 hWindow.SetPart 直接设置视口，
        /// 导致 ImgCol1/ImgCol2 等内部字段未更新，ZoomImage 会除以零产生 NaN。
        /// 在缩放/平移前调用此方法确保内部字段与实际视口一致。
        /// </summary>
        private void SyncViewportFromWindow()
        {
            try
            {
                if (ViewPort == null) return;
                var hWindow = ViewPort.HalconWindow;
                if (hWindow == null) return;
                HOperatorSet.GetPart(hWindow, out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                ImgRow1 = row1.D;
                ImgCol1 = col1.D;
                ImgRow2 = row2.D;
                ImgCol2 = col2.D;
            }
            catch { }
        }

        private void ZoomImage(double x, double y, double scale)
        {
            //关闭鼠标事件
            if (drawModel)
            {
                return;
            }
            // 同步内部视口字段，避免除以零产生 NaN
            if (ImgCol2 - ImgCol1 <= 0 || ImgRow2 - ImgRow1 <= 0)
            {
                SyncViewportFromWindow();
            }
            double lengthC, lengthR, percentC, percentR;
            int lenC, lenR;
            double viewportW = ImgCol2 - ImgCol1;
            double viewportH = ImgRow2 - ImgRow1;
            if (viewportW <= 0 || viewportH <= 0) return;

            percentC = (x - ImgCol1) / viewportW;
            percentR = (y - ImgRow1) / viewportH;

            lengthC = viewportW * scale;
            lengthR = viewportH * scale;

            ImgCol1 = x - lengthC * percentC;
            ImgCol2 = x + lengthC * (1 - percentC);

            ImgRow1 = y - lengthR * percentR;
            ImgRow2 = y + lengthR * (1 - percentR);

            lenC = (int)Math.Round(lengthC);
            lenR = (int)Math.Round(lengthR);

            System.Drawing.Rectangle rect = ViewPort.ImagePart;
            rect.X = (int)Math.Round(ImgCol1);
            rect.Y = (int)Math.Round(ImgRow1);
            rect.Width = (lenC > 0) ? lenC : 1;
            rect.Height = (lenR > 0) ? lenR : 1;



            ViewPort.ImagePart = rect;

            double _zoomWndFactor = 1;
            _zoomWndFactor = scale * ZoomWndFactor;

            if (ZoomWndFactor < 0.001 && _zoomWndFactor < ZoomWndFactor)
            {
                //����һ�����ű����Ͳ�������
                ResetWindow();
                return;
            }
            if (ZoomWndFactor > 100 && _zoomWndFactor > ZoomWndFactor)
            {
                //����һ�����ű����Ͳ�������
                ResetWindow();
                return;
            }
            ZoomWndFactor = _zoomWndFactor;

            Repaint();
        }
        /// <summary>
        /// Scales the image in the HALCON window according to the 
        /// value scaleFactor
        /// </summary>
        public void ZoomImage(double scaleFactor)
        {

            double midPointX, midPointY;

            if (((ImgRow2 - ImgRow1) == scaleFactor * ImageHeight) &&
                ((ImgCol2 - ImgCol1) == scaleFactor * ImageWidth))
            {
                Repaint();
                return;
            }

            ImgRow2 = ImgRow1 + ImageHeight;
            ImgCol2 = ImgCol1 + ImageWidth;

            midPointX = ImgCol1;
            midPointY = ImgRow1;

            ZoomWndFactor = (double)ImageWidth / ViewPort.Width;
            ZoomImage(midPointX, midPointY, scaleFactor);
        }
        /// <summary>
        /// Scales the HALCON window according to the value scale
        /// </summary>
        public void ScaleWindow(double scale)
        {
            ImgRow1 = 0;
            ImgCol1 = 0;

            ImgRow2 = ImageHeight;
            ImgCol2 = ImageWidth;

            ViewPort.Width = (int)(ImgCol2 * scale);
            ViewPort.Height = (int)(ImgRow2 * scale);

            ZoomWndFactor = ((double)ImageWidth / ViewPort.Width);
        }
        /// <summary>
        /// Recalculates the image-window-factor, which needs to be added to 
        /// the scale factor for zooming an image. This way the zoom gets 
        /// adjusted to the window-image relation, expressed by the equation 
        /// ImageWidth/ViewPort.Width.
        /// </summary>
        public void SetZoomWndFactor()
        {
            ZoomWndFactor = ((double)ImageWidth / ViewPort.Width);
        }
        /// <summary>
        /// Sets the image-window-factor to the value zoomF
        /// </summary>
        public void SetZoomWndFactor(double zoomF)
        {
            ZoomWndFactor = zoomF;
        }

        /// <summary>
        /// 获取当前窗口的缩放因子（屏幕像素 / 图像坐标视口宽度）
        /// 放大时值变大，缩小时值变小，用于 ROI 手柄自适应大小计算
        /// 注意：直接从 HalconWindow 读取 GetPart，避免依赖可能未同步的内部字段
        /// </summary>
        /// <returns>缩放因子，正常范围 ≥ 0</returns>
        public double GetZoomFactor()
        {
            try
            {
                if (ViewPort == null) return 1.0;
                var hWindow = ViewPort.HalconWindow;
                if (hWindow == null) return 1.0;

                // 直接从 HalconWindow 读取当前视口范围（图像坐标）
                HOperatorSet.GetPart(hWindow, out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                double viewportWidth = col2.D - col1.D;
                if (viewportWidth <= 0) return 1.0;

                // 使用控件的屏幕像素宽度
                double windowWidth = ViewPort.Width;
                if (windowWidth <= 0) return 1.0;

                return windowWidth / viewportWidth;
            }
            catch
            {
                return 1.0;
            }
        }
        /*******************************************************************/
        private void MoveImage(double MotionX, double MotionY)
        {
            // 同步内部视口字段，避免基于未初始化的值平移
            if (ImgCol2 - ImgCol1 <= 0 || ImgRow2 - ImgRow1 <= 0)
            {
                SyncViewportFromWindow();
            }
            ImgRow1 += -MotionY;
            ImgRow2 += -MotionY;

            ImgCol1 += -MotionX;
            ImgCol2 += -MotionX;

            System.Drawing.Rectangle rect = ViewPort.ImagePart;
            rect.X = (int)Math.Round(ImgCol1);
            rect.Y = (int)Math.Round(ImgRow1);
            ViewPort.ImagePart = rect;

            Repaint();
        }
        /// <summary>
        /// Resets all parameters that concern the HALCON window display 
        /// setup to their initial values and clears the ROI list.
        /// </summary>
        protected internal void ResetAll()
        {
            ImgRow1 = 0;
            ImgCol1 = 0;
            ImgRow2 = ImageHeight;
            ImgCol2 = ImageWidth;

            ZoomWndFactor = (double)ImageWidth / ViewPort.Width;

            System.Drawing.Rectangle rect = ViewPort.ImagePart;
            rect.X = (int)ImgCol1;
            rect.Y = (int)ImgRow1;
            rect.Width = (int)ImageWidth;
            rect.Height = (int)ImageHeight;
            ViewPort.ImagePart = rect;


            if (ROIManager != null)
                ROIManager.ResetVar();
        }
        protected internal void ResetWindow(bool fitImage = true)
        {

            if (ImageHeight == 0)
            {
                return;
            }
            double ratio_win = (double)ViewPort.WindowSize.Width / (double)ViewPort.WindowSize.Height;
            double ratio_img = (double)ImageWidth / (double)ImageHeight;

            int _beginRow, _begin_Col, _endRow, _endCol;
            //
            if (ratio_win >= ratio_img)
            {
                _beginRow = 0;
                _endRow = ImageHeight - 1;
                _begin_Col = (int)(-ImageWidth * (ratio_win / ratio_img - 1d) / 2d);
                _endCol = (int)(ImageWidth + ImageWidth * (ratio_win / ratio_img - 1d) / 2d);
            }
            else
            {
                _begin_Col = 0;
                _endCol = ImageWidth - 1;
                _beginRow = (int)(-ImageHeight * (ratio_img / ratio_win - 1d) / 2d);
                _endRow = (int)(ImageHeight + ImageHeight * (ratio_img / ratio_win - 1d) / 2d);
            }
            //���ű���Ϊ1
            ZoomWndFactor = 1;

            System.Drawing.Rectangle rect = ViewPort.ImagePart;
            if (fitImage)
            {
                rect.X = (int)_begin_Col;
                rect.Y = (int)_beginRow;
                rect.Width = (int)_endCol - _begin_Col;
                rect.Height = (int)_endRow - _beginRow;
                ViewPort.ImagePart = rect;
            }
            else
            {
                SetImagePart(0, 0, ImageHeight, ImageWidth);
            }
            ImgRow1 = _beginRow;
            ImgCol1 = _begin_Col;
            ImgRow2 = _endRow;
            ImgCol2 = _endCol;
        }
        /*************************************************************************/
        /*      			 Event handling for mouse	   	                     */
        /*************************************************************************/
        private void MouseDown(object sender, HMouseEventArgs e)
        {
            //�ر������¼�
            if (drawModel) return;
            Repaint();
            ViewPort.Cursor = System.Windows.Forms.Cursors.Hand;
            ViewState = MODE_VIEW_MOVE;
            MousePressed = true;
            string ActiveROIId = "";
            if (ROIManager != null && (DispROI == MODE_INCLUDE_ROI))
            {
                ActiveROIId = ROIManager.mouseDownAction(e.X, e.Y);
            }
            switch (ViewState)
            {
                case MODE_VIEW_MOVE:
                    StartX = e.X;
                    StartY = e.Y;
                    break;

                case MODE_VIEW_NONE:
                    break;
                case MODE_VIEW_ZOOMWINDOW:
                    ActivateZoomWindow((int)e.X, (int)e.Y);
                    break;
                default:
                    break;
            }
            //end of if
        }

        /*******************************************************************/
        private void ActivateZoomWindow(int X, int Y)
        {
            double posX, posY;
            int ZoomZone;

            if (ZoomWindow != null)
                ZoomWindow.Dispose();

            HOperatorSet.SetSystem("border_width", 10);
            ZoomWindow = new HWindow();

            posX = ((X - ImgCol1) / (ImgCol2 - ImgCol1)) * ViewPort.Width;
            posY = ((Y - ImgRow1) / (ImgRow2 - ImgRow1)) * ViewPort.Height;

            ZoomZone = (int)((ZoomWndSize / 2) * ZoomWndFactor * ZoomAddOn);
            ZoomWindow.OpenWindow((int)(posY - (ZoomWndSize / 2)), (int)(posX - (ZoomWndSize / 2)), (int)ZoomWndSize, (int)ZoomWndSize,
                                   ViewPort.HalconID, "visible", "");
            ZoomWindow.SetPart(Y - ZoomZone, X - ZoomZone, Y + ZoomZone, X + ZoomZone);
            Repaint(ZoomWindow);
            ZoomWindow.SetColor("black");
        }
        public void RaiseMouseup()
        {
            MousePressed = false;

            if (ROIManager != null && (ROIManager.ActiveROIId.Length > 0) && (DispROI == MODE_INCLUDE_ROI))
            {
                ROIManager.NotifyRCObserver(EVENT_UPDATE_ROI);
            }
            else if (ViewState == MODE_VIEW_ZOOMWINDOW)
            {
                ZoomWindow.Dispose();
            }
        }
        /*******************************************************************/
        private void MouseUp(object sender, HMouseEventArgs e)
        {
            //�ر������¼�
            if (drawModel)
            {
                return;
            }

            MousePressed = false;

            if (ROIManager != null && (ROIManager.ActiveROIId.Length > 0) && (DispROI == MODE_INCLUDE_ROI))
            {
                ROIManager.NotifyRCObserver(EVENT_UPDATE_ROI);
            }
            else if (ViewState == MODE_VIEW_ZOOMWINDOW)
            {
                ZoomWindow.Dispose();
            }
            PaintCrossEvent?.Invoke();
        }
        /*******************************************************************/
        private void MouseMoved(object sender, HMouseEventArgs e)
        {
            //�ر������¼�
            if (drawModel) { return; }
            double currX, currY;
            try
            {
                if (HObjList.Count < 1 || HObjList[0].HObj == null || (HObjList[0].HObj is HImage) == false)
                {
                    return;
                }
                ViewPort.HalconWindow.GetMpositionSubPix(out currY, out currX, out int state);
                HImage hv_image = HObjList[0].HObj as HImage;
                if (hv_image == null) return;
                bool _isXOut = true, _isYOut = true;
                string str_imgSize = string.Format("W:{0}*H:{1}", ImageWidth, ImageHeight);
                int channel_count = hv_image.CountChannels();
                string str_position = string.Format("|X:{0:F0},Y:{1:F0}", currX, currY);
                _isXOut = (currX < 0 || currX >= ImageWidth);
                _isYOut = (currY < 0 || currY >= ImageHeight);
                //��ȡͼƬ��ǰ���λ�ûҶ�ֵ��
                string str_value = "";
                if (!_isXOut && !_isYOut)
                {
                    if (channel_count == 1)
                    {
                        double grayVal;
                        grayVal = hv_image.GetGrayval((int)currY, (int)currX);
                        str_value = string.Format("|GrayVal:{0}", grayVal);
                    }
                    else if ((int)channel_count == 3)
                    {
                        double grayValRed, grayValGreen, grayValBlue;
                        HImage _RedChannel, _GreenChannel, _BlueChannel;
                        _RedChannel = hv_image.AccessChannel(1);
                        _GreenChannel = hv_image.AccessChannel(2);
                        _BlueChannel = hv_image.AccessChannel(3);
                        grayValRed = _RedChannel.GetGrayval((int)currY, (int)currX);
                        grayValGreen = _GreenChannel.GetGrayval((int)currY, (int)currX);
                        grayValBlue = _BlueChannel.GetGrayval((int)currY, (int)currX);
                        str_value = string.Format("| R:{0}, G:{1}, B:{2})", grayValRed, grayValGreen, grayValBlue);
                    }
                    message = str_imgSize + str_position + str_value;
                    if (message.Length > 0)
                    {
                        TriggerShowMessageEvent(new ShowMessageEventArgs(message));
                    }
                    switch (StateView)
                    {
                        case MODE_ERASER://��������
                            if (state == 1)
                            {   //ˢ��
                                BrushRegion = ROIManager.Eraser(currY, currX, ZoomWndFactor);
                            }
                            break;
                        case MODE_PAINT://��Ϳ����
                            if (state == 1)
                            {   //ˢ��
                                BrushRegion = ROIManager.Paint(currY, currX, ZoomWndFactor);
                            }
                            break;
                        case MODE_VIEW_NONE:
                            //ˢ��
                            BrushRegion = new HRegion(0, 0, 0.0);
                            break;
                    }
                    Repaint();
                }
            }
            catch (Exception ex)
            {
                Repaint();
            }
            double MotionX, MotionY, PosX, PosY, ZoomZone;
            if (!MousePressed) return;
            if (ROIManager != null && (ROIManager.ActiveROIId.Length > 0) && (DispROI == MODE_INCLUDE_ROI))
            {
                ROIManager.mouseMoveAction(e.X, e.Y);
            }
            else if (ViewState == MODE_VIEW_MOVE)//ƽ��ͼ��
            {
                if (!isMoveEnable)
                {
                    return;
                }
                MotionX = (e.X - StartX);
                MotionY = (e.Y - StartY);

                if (((int)MotionX != 0) || ((int)MotionY != 0))
                {
                    MoveImage(MotionX, MotionY);
                    StartX = e.X - MotionX;
                    StartY = e.Y - MotionY;
                }
            }
            else if (ViewState == MODE_VIEW_ZOOMWINDOW)//�ֲ��Ŵ�
            {
                HSystem.SetSystem("flush_graphic", "false");
                ZoomWindow.ClearWindow();
                PosX = ((e.X - ImgCol1) / (ImgCol2 - ImgCol1)) * ViewPort.Width;
                PosY = ((e.Y - ImgRow1) / (ImgRow2 - ImgRow1)) * ViewPort.Height;
                ZoomZone = (ZoomWndSize / 2) * ZoomWndFactor * ZoomAddOn;
                ZoomWindow.SetWindowExtents((int)(PosY - (ZoomWndSize / 2)), (int)(PosX - (ZoomWndSize / 2)), ZoomWndSize, ZoomWndSize);
                ZoomWindow.SetPart((int)(e.Y - ZoomZone), (int)(e.X - ZoomZone), (int)(e.Y + ZoomZone), (int)(e.X + ZoomZone));
                Repaint(ZoomWindow);
                HSystem.SetSystem("flush_graphic", "true");
                ZoomWindow.DispLine(-100.0, -100.0, -100.0, -100.0);
            }
        }

        /// <summary>
        /// To initialize the move function using a GUI component, the HWndCtrl
        /// first needs to know the range supplied by the GUI component. 
        /// For the x direction it is specified by xRange, which is 
        /// calculated as follows: GuiComponentX.Max()-GuiComponentX.Min().
        /// The starting value of the GUI component has to be supplied 
        /// by the parameter Init
        /// </summary>
        public void SetGUICompRangeX(int[] xRange, int Init)
        {
            CompRangeX = xRange;
            int RangeX = xRange[1] - xRange[0];
            PrevCompX = Init;
            StepSizeX = ((double)ImageWidth / RangeX) * (ImageWidth / WindowWidth);
        }

        /// <summary>
        /// To initialize the move function using a GUI component, the HWndCtrl
        /// first needs to know the range supplied by the GUI component. 
        /// For the y direction it is specified by yRange, which is 
        /// calculated as follows: GuiComponentY.Max()-GuiComponentY.Min().
        /// The starting value of the GUI component has to be supplied 
        /// by the parameter Init
        /// </summary>
        public void SetGUICompRangeY(int[] yRange, int Init)
        {
            CompRangeY = yRange;
            int RangeY = yRange[1] - yRange[0];
            PrevCompY = Init;
            StepSizeY = ((double)ImageHeight / RangeY) * (ImageHeight / WindowHeight);
        }

        /// <summary>
        /// Resets to the starting value of the GUI component.
        /// </summary>
        public void ResetGUIInitValues(int xVal, int yVal)
        {
            PrevCompX = xVal;
            PrevCompY = yVal;
        }

        /// <summary>
        /// Moves the image by the value valX supplied by the GUI component
        /// </summary>
        public void MoveXByGUIHandle(int valX)
        {
            double MotionX;

            MotionX = (valX - PrevCompX) * StepSizeX;

            if (MotionX == 0)
                return;

            MoveImage(MotionX, 0.0);
            PrevCompX = valX;
        }

        /// <summary>
        /// Moves the image by the value valY supplied by the GUI component
        /// </summary>
        public void MoveYByGUIHandle(int valY)
        {
            double MotionY;

            MotionY = (valY - PrevCompY) * StepSizeY;

            if (MotionY == 0)
                return;

            MoveImage(0.0, MotionY);
            PrevCompY = valY;
        }

        /// <summary>
        /// Zooms the image by the value valF supplied by the GUI component
        /// </summary>
        public void ZoomByGUIHandle(double valF)
        {
            double x, y, scale;
            double prevScaleC;

            x = (ImgCol1 + (ImgCol2 - ImgCol1) / 2);
            y = (ImgRow1 + (ImgRow2 - ImgRow1) / 2);

            prevScaleC = (double)((ImgCol2 - ImgCol1) / ImageWidth);
            scale = ((double)1.0 / prevScaleC * (100.0 / valF));

            ZoomImage(x, y, scale);
        }
        /// <summary>
        /// Triggers a Repaint of the HALCON window
        /// </summary>
        public void Repaint()
        {
            Repaint(ViewPort.HalconWindow);
        }
        public void ClearROI()
        {
            try
            {
                ViewPort.HalconWindow.SetDraw("margin");
                HSystem.SetSystem("flush_graphic", "false");
                ViewPort.HalconWindow.ClearWindow();
                mGC.stateOfSettings.Clear();
                //��ʾͼƬ
                for (int i = 0; i < HObjList.Count; i++)
                {
                    HObjectEntry entry = (HObjectEntry)HObjList[i];
                    mGC.ApplyContext(ViewPort.HalconWindow, entry.gContext);
                    ViewPort.HalconWindow.DispObj(entry.HObj);
                }
                //��ʾregion
                //ShowHObjectList();
                //AddInfoDelegate();
                //if (ROIManager != null && (DispROI == MODE_INCLUDE_ROI))
                //{
                //    ROIManager.PaintData(ViewPort.HalconWindow);
                //}
                HSystem.SetSystem("flush_graphic", "true");
                //ע�����������,�ᵼ�´����޷�ʵ�����ź��϶�
                ViewPort.HalconWindow.SetColor("black");
                ViewPort.HalconWindow.DispLine(-100.0, -100.0, -101.0, -101.0);
            }
            catch (Exception ex)
            {
            }
        }
        /// <summary>
        /// Repaints the HALCON window 'window'
        /// </summary>
        public void Repaint(HWindow window)
        {
            try
            {
                window.SetDraw("margin");
                HSystem.SetSystem("flush_graphic", "false");
                window.ClearWindow();
                mGC.stateOfSettings.Clear();
                //��ʾͼƬ
                for (int i = 0; i < HObjList.Count; i++)
                {
                    HObjectEntry entry = (HObjectEntry)HObjList[i];
                    mGC.ApplyContext(window, entry.gContext);
                    window.DispObj(entry.HObj);
                }
                //��ʾregion
                int scale = (int)((double)(ViewPort.Width) * ZoomWndFactor);
                ShowHObjectList();
                AddInfoDelegate();
                if (ROIManager != null && (DispROI == MODE_INCLUDE_ROI))
                {
                    ROIManager.PaintData(window, imageWidth);
                }
                ShowTransientOverlay(window);
                HSystem.SetSystem("flush_graphic", "true");
                //ע�����������,�ᵼ�´����޷�ʵ�����ź��϶�
                window.SetColor("black");
                window.DispLine(-100.0, -100.0, -101.0, -101.0);
                // ����ʮ���ػ�
                PaintCrossEvent?.Invoke();
            }
            catch (Exception ex)
            {
            }
        }

        /*******************************************************************
        /*                      GRAPHICSSTACK                               */
        /********************************************************************/

        /// <summary>
        /// Adds an iconic object to the graphics stack similar to the way
        /// it is defined for the HDevelop graphics stack.
        /// </summary>
        /// <param name="obj">Iconic object</param>
        public void AddIconicVar(HObject img)
        {
            //�Ȱ�HObjImageList��ȫ���ͷ���,Դ���� ������ڴ�й©����
            for (int i = 0; i < HObjList.Count; i++)
            {
                ((HObjectEntry)HObjList[i]).clear();
            }
            if (img == null|| !img.IsInitialized()) return;
            HOperatorSet.GetObjClass(img, out HTuple classValue);
            if (classValue.Length == 0) return;
            if (!classValue.S.Equals("image"))
            { return; }
            if ((HImage)img is HImage)
            {
                int area = ((HImage)img).GetDomain().AreaCenter(out double r, out double c);
                ((HImage)img).GetImagePointer1(out string s, out int w, out int h);

                if (area == (w * h))
                {
                    ClearList();
                    if (w != ImageWidth || h != ImageHeight)
                    {
                        ImageWidth = w;
                        ImageHeight = h;
                        ZoomWndFactor = (double)ImageWidth / ViewPort.Width;
                        SetImagePart(0, 0, h, w);
                    }
                }//if
            }//if
            HObjectEntry entry = new HObjectEntry((HImage)img, mGC.copyContextList());
            HObjList.Add(entry);
            //ÿ�����뱳��ͼ��ʱ�� �����HObjectList
            ClearHObjectList();
            if (HObjList.Count > MAXNUMOBJLIST)
            {
                //��Ҫ�Լ��ֶ��ͷ�
                ((HObjectEntry)HObjList[0]).clear();
                HObjList.RemoveAt(1);
            }
        }

        /// <summary>
        /// Clears all entries from the graphics stack 
        /// </summary>
        public void ClearList()
        {
            HObjList.Clear();
        }

        /// <summary>
        /// Returns the number of items on the graphics stack
        /// </summary>
        public int GetListCount()
        {
            return HObjList.Count;
        }

        /// <summary>
        /// Changes the current graphical context by setting the specified mode
        /// (constant starting by GC_*) to the specified value.
        /// </summary>
        /// <param name="mode">
        /// Constant that is provided by the class GraphicsContext
        /// and describes the mode that has to be changed, 
        /// e.g., GraphicsContext.GC_COLOR
        /// </param>
        /// <param name="val">
        /// Value, provided as a string, 
        /// the mode is to be changed to, e.g., "blue" 
        /// </param>
        public void ChangeGraphicSettings(string mode, string val)
        {
            switch (mode)
            {
                case GraphicsContext.GC_COLOR:
                    mGC.setColorAttribute(val);
                    break;
                case GraphicsContext.GC_DRAWMODE:
                    mGC.setDrawModeAttribute(val);
                    break;
                case GraphicsContext.GC_LUT:
                    mGC.setLutAttribute(val);
                    break;
                case GraphicsContext.GC_PAINT:
                    mGC.setPaintAttribute(val);
                    break;
                case GraphicsContext.GC_SHAPE:
                    mGC.setShapeAttribute(val);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Changes the current graphical context by setting the specified mode
        /// (constant starting by GC_*) to the specified value.
        /// </summary>
        /// <param name="mode">
        /// Constant that is provided by the class GraphicsContext
        /// and describes the mode that has to be changed, 
        /// e.g., GraphicsContext.GC_LINEWIDTH
        /// </param>
        /// <param name="val">
        /// Value, provided as an integer, the mode is to be changed to, 
        /// e.g., 5 
        /// </param>
        public void ChangeGraphicSettings(string mode, int val)
        {
            switch (mode)
            {
                case GraphicsContext.GC_COLORED:
                    mGC.setColoredAttribute(val);
                    break;
                case GraphicsContext.GC_LINEWIDTH:
                    mGC.setLineWidthAttribute(val);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Changes the current graphical context by setting the specified mode
        /// (constant starting by GC_*) to the specified value.
        /// </summary>
        /// <param name="mode">
        /// Constant that is provided by the class GraphicsContext
        /// and describes the mode that has to be changed, 
        /// e.g.,  GraphicsContext.GC_LINESTYLE
        /// </param>
        /// <param name="val">
        /// Value, provided as an HTuple instance, the mode is 
        /// to be changed to, e.g., new HTuple(new int[]{2,2})
        /// </param>
        public void ChangeGraphicSettings(string mode, HTuple val)
        {
            switch (mode)
            {
                case GraphicsContext.GC_LINESTYLE:
                    mGC.setLineStyleAttribute(val);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Clears all entries from the graphical context list
        /// </summary>
        public void ClearGraphicContext()
        {
            mGC.clear();
        }

        /// <summary>
        /// Returns a clone of the graphical context list (hashtable)
        /// </summary>
        public Hashtable GetGraphicContext()
        {
            return mGC.copyContextList();
        }

        /// <summary>
        /// Registers an instance of an ROIController with this window 
        /// controller (and vice versa).
        /// </summary>
        /// <param name="rC"> 
        /// Controller that manages interactive ROIs for the HALCON window 
        /// </param>
        protected internal void SetROIController(ROIController rC)
        {
            ROIManager = rC;
            rC.SetViewController(this);
            this.SetViewState(HWndCtrl.MODE_VIEW_NONE);
        }
        /// <summary>
        /// ����趨��ʾ��ͼ��
        /// </summary>
        /// <param name="image"></param>
        protected internal void addImageShow(HObject image)
        {
            if (image == null || !image.IsInitialized()) return;
            AddIconicVar(image);
        }
        #region �ٴ���ʾregion�� xld

        /// <summary>
        /// hObjectList�����洢�����HObject
        /// </summary>
        private List<HObjectWithColor> hObjectList = new List<HObjectWithColor>();
        /// <summary>
        /// roiTextList�����洢�������ʾ�ı�
        /// </summary>
        private List<HText> roiTextList = new List<HText>();
        // 独立的瞬态叠加层：用于表格选中等高频交互，避免重建所有常驻图形对象。
        private HObjectWithColor transientOverlay;

        /// <summary>
        /// Ĭ�Ϻ���ɫ��ʾ
        /// </summary>
        /// <param name="hObj">�����region.xld,image</param>
        public void DispObj(HObject hObj,bool isFillDis)
        {
            DispObj(hObj, null, isFillDis);
        }

        /// <summary>
        /// ���¿����ڴ汣�� ��ֹ�������HObject�������ط�dispose��,��������
        /// </summary>
        /// <param name="hObj">�����region.xld,image</param>
        /// <param name="color">��ɫ</param>
        public void DispObj(HObject hObj, string color,bool isFillDisp)
        {
            lock (this)
            {
                try
                {
                    //��ʾָ������ɫ
                    if (color != null)
                    {
                        HOperatorSet.SetColor(ViewPort.HalconWindow, color);
                    }
                    else
                    {
                        HOperatorSet.SetColor(ViewPort.HalconWindow, "red");
                    }
                    if (hObj != null && hObj.IsInitialized())
                    {
                        HObject temp = new HObject(hObj);
                        hObjectList.Add(new HObjectWithColor(temp, color, isFillDisp));
                        if (isFillDisp)
                            HOperatorSet.SetDraw(ViewPort.HalconWindow, "fill");
                        else
                            HOperatorSet.SetDraw(ViewPort.HalconWindow, "margin");
                        ViewPort.HalconWindow.DispObj(temp);
                    }
                    //�ָ�Ĭ�ϵĺ�ɫ
                    HOperatorSet.SetColor(ViewPort.HalconWindow, "red");
                }
                catch { }
            }
        }

        /// <summary>
        /// ���¿����ڴ汣�� ��ֹ�������HObject�������ط�dispose��,��������
        /// </summary>
        /// <param name="hObj">�����region.xld,image</param>
        /// <param name="color">��ɫ</param>
        public void DispObj(HText roiText)
        {
            lock (this)
            {
                roiTextList.Add(roiText);
                ShowTool.SetFont(ViewPort.HalconWindow, roiText.size, "false", "false");
                ShowTool.SetMsg(ViewPort.HalconWindow, roiText.text, "image", roiText.row, roiText.col, roiText.drawColor, "false");
            }
        }

        /// <summary>
        /// ÿ�δ����µı���Imageʱ,���hObjectList,�����ڴ�û�б��ͷ�
        /// </summary>
        public void ClearHObjectList()
        {

            foreach (HObjectWithColor hObjectWithColor in hObjectList)
            {
                hObjectWithColor.HObject.Dispose();
            }
            hObjectList.Clear();
            roiTextList.Clear();
        }

        /// <summary>
        /// 设置窗口顶部的瞬态叠加对象。该对象不进入常驻叠加层，适用于频繁变化的选中高亮；
        /// 每次替换仅重绘窗口，不会清空或重新生成已有ROI、骨架和业务叠加层。
        /// </summary>
        public void SetTransientOverlay(HObject hObj, string color)
        {
            lock (this)
            {
                transientOverlay?.HObject?.Dispose();
                transientOverlay = null;

                if (hObj != null && hObj.IsInitialized())
                    transientOverlay = new HObjectWithColor(new HObject(hObj), color, false);

                Repaint();
            }
        }

        /// <summary>
        /// ��hObjectList�е�HObject,�����Ⱥ�˳����ʾ����
        /// </summary>
        private void ShowHObjectList()
        {
            try
            {
                foreach (HObjectWithColor hObjectWithColor in hObjectList)
                {
                    if (hObjectWithColor.Color != null)
                    {
                        HOperatorSet.SetColor(ViewPort.HalconWindow, hObjectWithColor.Color);
                    }
                    else
                    {
                        HOperatorSet.SetColor(ViewPort.HalconWindow, "red");
                    }
                    if (hObjectWithColor != null && hObjectWithColor.HObject.IsInitialized())
                    {
                        if (hObjectWithColor.IsFill)
                        {
                            HOperatorSet.SetDraw(ViewPort.HalconWindow, "fill");
                        }
                        else
                        {
                            HOperatorSet.SetDraw(ViewPort.HalconWindow, "margin");
                        }
                        ViewPort.HalconWindow.DispObj(hObjectWithColor.HObject);
                        //�ָ�Ĭ�ϵĺ�ɫ
                        HOperatorSet.SetColor(ViewPort.HalconWindow, "red");
                    }
                }

                foreach (HText roiText in roiTextList)
                {
                    ShowTool.SetFont(ViewPort.HalconWindow, roiText.size, "false", "false");
                    ShowTool.SetMsg(ViewPort.HalconWindow, roiText.text, "image", roiText.row, roiText.col, roiText.drawColor, "false");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                //��ʱ��hobj��dispose��,�����䱾���Ϊnull,��ʱ�򱨴�. �Ѿ�ʹ��IsInitialized����� 
            }
        }

        /// <summary>在常驻图形和交互ROI之后绘制瞬态叠加层，确保选中高亮始终位于最上层。</summary>
        private void ShowTransientOverlay(HWindow window)
        {
            if (transientOverlay?.HObject == null || !transientOverlay.HObject.IsInitialized())
                return;

            window.SetDraw(transientOverlay.IsFill ? "fill" : "margin");
            window.SetColor(string.IsNullOrEmpty(transientOverlay.Color) ? "red" : transientOverlay.Color);
            window.DispObj(transientOverlay.HObject);
        }

        #endregion
    }//end of class
}//end of namespace
