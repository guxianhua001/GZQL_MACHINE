using System;
using HalconDotNet;
using System.Collections;
using System.Collections.Generic;

namespace HalconWrapper.Model
{

    public delegate void FuncROIDelegate();
    /// <summary>
    /// This class creates and manages ROI objects. It responds 
    /// to  mouse device inputs using the methods mouseDownAction and 
    /// mouseMoveAction. You don't have to know this class in detail when you 
    /// build your own C# Proj. But you must consider a few things if 
    /// you want to use interactive ROIs in your application: There is a
    /// quite close connection between the ROIController and the HWndCtrl 
    /// class, which means that you must 'register' the ROIController
    /// with the HWndCtrl, so the HWndCtrl knows it has to forward user input
    /// (like mouse events) to the ROIController class.  
    /// The visualization and manipulation of the ROI objects is done 
    /// by the ROIController.
    /// This class provides special support for the matching
    /// applications by calculating a model region from the list of ROIs. For
    /// this, ROIs are added and subtracted according to their sign.
    /// </summary>
    public class ROIController
    {
        ///<summary>��Ϳ����</summary>
        public HRegion BrushRegion;
        ///<summary>��Ĥ����</summary>
        public HRegion MaskRegion;
        /// <summary>������ĿǰΪֹ���д�����ROI������б�</summary>
        public Dictionary<string, ROI> ROIList;
        /// <summary> ROI���� </summary>
        private ROI ROIMode;
        /// <summary> ROI��ʽ </summary>
        private int ROIState;
        /// <summary> ROI���� </summary>
        private string ROIName;
        /// <summary>ROI���� </summary>
        public HRegion ROIModel;
        private double currX, currY;
        /// <summary>Index of the active ROI object</summary>
        public string ActiveROIId;
        public string DeleteROIId;
        /// <summary>���������ɫ </summary>
        private string ActiveCol = "cyan";
        /// <summary> ����С����ɫ </summary>
        private string ActiveROICol = "red";
        /// <summary> ����϶���ɫ</summary>
        private string ActiveMousCol = "blue";
        /// <summary>�ο�HWndCtrl, ROI������ע�ᵽ </summary>
        public HWndCtrl viewController;
        /// <summary> ί�У���֪ͨ��ģ�������������ĸ��� </summary>
        public IconicDelegate NotifyRCObserver;

        /// <summary>���캯��</summary>
        protected internal ROIController()
        {
            ROIState = HWndCtrl.MODE_ROI_NONE;
            ROIModel = new HRegion();
            ROIList = new Dictionary<string, ROI>();
            NotifyRCObserver = new IconicDelegate(dummyI);
            ActiveROIId = DeleteROIId = "";
            currX = currY = -1;
        }
        /// <summary>Registers the HWndCtrl to this ROIController instance</summary>
        public void SetViewController(HWndCtrl view)
        {
            viewController = view;
        }
        /// <summary>Gets the ROIModel object</summary>
        public HRegion GetModelRegion()
        {
            return ROIModel;
        }
        /// <summary>Gets the List of ROIs created so far</summary>
        public Dictionary<string, ROI> GetROIList()
        {
            return ROIList;
        }
        /// <summary>Get the active ROI</summary>
        public ROI GetActiveROI()
        {
            try
            {
                if (ActiveROIId.Length > 0)
                    foreach (KeyValuePair<string, ROI> kvp in ROIList)
                    {
                        if (kvp.Key.Equals(ActiveROIId))
                        {
                            return kvp.Value;
                        }
                    }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
        public string GetActiveROIId()
        {
            return ActiveROIId;
        }
        public void SetActiveROIId(string active)
        {
            ActiveROIId = active;
        }
        public string GetDelROIId()
        {
            return DeleteROIId;
        }
        /// <summary> ����ROI��ʽ </summary>
        public void SetROIShape(ROI r)
        {
            ROIMode = r;
            ROIMode.SetOperatorFlag(ROIState);
        }
        /// <summary> ����ROI��ʽ </summary>
        public void SetROISign(int mode)
        {
            ROIState = mode;
            if (ActiveROIId.Length > 0)
            {
                foreach (KeyValuePair<string, ROI> kvp in ROIList)
                {
                    if (kvp.Key.Equals(ActiveROIId))
                    {
                        kvp.Value.SetOperatorFlag(ROIState);
                    }
                }
                viewController.Repaint();
                NotifyRCObserver(HWndCtrl.EVENT_CHANGED_ROI_SIGN);
            }
        }

        /// <summary>
        /// Removes the ROI object that is marked as active. 
        /// If no ROI object is active, then nothing happens. 
        /// </summary>
        public void RemoveActive()
        {
            if (ActiveROIId.Length > 0)
            {
                ROIList.Remove(ActiveROIId);
                DeleteROIId = ActiveROIId;
                ActiveROIId = "";
                viewController.Repaint();
                NotifyRCObserver(HWndCtrl.EVENT_DELETED_ACTROI);
            }
        }
        /// <summary>
        ///������������ж����ROIModel����
        ///��ROIList�У�ͨ���Ӽ�������
        ///����ROI����
        /// </summary>
        public bool DefineModelROI()
        {
            HRegion tmpAdd, tmpDiff, tmp;
            double row, col;
            if (ROIState == HWndCtrl.MODE_ROI_NONE)
                return true;
            tmpAdd = new HRegion();
            tmpDiff = new HRegion();
            tmpAdd.GenEmptyRegion();
            tmpDiff.GenEmptyRegion();
            foreach (KeyValuePair<string, ROI> kvp in ROIList)
            {
                switch (kvp.Value.GetOperatorFlag())
                {
                    case ROI.POSITIVE_FLAG:
                        tmp = kvp.Value.GetRegion();
                        tmpAdd = tmp.Union2(tmpAdd);
                        break;
                    case ROI.NEGATIVE_FLAG:
                        tmp = kvp.Value.GetRegion();
                        tmpDiff = tmp.Union2(tmpDiff);
                        break;
                    default:
                        break;
                }
            }
            ROIModel = null;
            if (tmpAdd.AreaCenter(out row, out col) > 0)
            {
                tmp = tmpAdd.Difference(tmpDiff);
                if (tmp.AreaCenter(out row, out col) > 0)
                    ROIModel = tmp;
            }
            //in case the set of positiv and negative ROIs dissolve 
            if (ROIModel == null || ROIList.Count == 0) return false;
            return true;
        }
        /// <summary>
        /// ������й���ROI����ı���
        /// </summary>
        public void ResetVar()
        {
            ROIList.Clear();
            ActiveROIId = "";
            ROIModel = null;
            ROIMode = null;
            NotifyRCObserver(HWndCtrl.EVENT_DELETED_ALL_ROIS);
        }
        /// <summary>
        ///���'seed' ROI�����ѱ����ݣ���ɾ����ROIʵ��
        ///ͨ��Ӧ�ó����ൽROIController��iables����ROI����
        /// </summary>
        public void ResetROI()
        {
            ActiveROIId = "";
            ROIMode = null;
        }

        /// <summary>����ROI�������ɫ</summary>
        /// <param name="aColor">Color for the active ROI object</param>
        /// <param name="inaColor">Color for the inactive ROI objects</param>
        /// <param name="aHdlColor">
        /// �����ROI����ļ���������ɫ
        /// </param>
        public void SetDrawColor(string aColor, string aHdlColor, string inaColor)
        {
            if (aColor != "")
                ActiveCol = aColor;
            if (aHdlColor != "")
                ActiveROICol = aHdlColor;
            if (inaColor != "")
                ActiveMousCol = inaColor;
        }
        /// <summary>��ROIList�е����ж�����Ƶ�HALCON������ </summary>
        /// <param name="window">HALCON window</param>
        public void PaintData(HWindow window, int imageWidth)
        {
            window.SetDraw("margin");
            window.SetLineWidth(1);
            if (ROIList.Count > 0)
            {
                window.SetDraw("margin");
                foreach (KeyValuePair<string, ROI> kvp in ROIList)
                {
                    kvp.Value.ImageWidth = imageWidth;
                    window.SetColor(kvp.Value.Color);
                    window.SetLineStyle(kvp.Value.FlagLineStyle);
                    kvp.Value.Draw(window);
                }

                if (ActiveROIId.Length > 0)
                {
                    window.SetColor(ActiveCol);
                    window.SetLineStyle(ROIList[ActiveROIId].FlagLineStyle);
                    ROIList[ActiveROIId].Draw(window);

                    window.SetColor(ActiveROICol);
                    ROIList[ActiveROIId].DisplayActive(window);
                }
            }
        }
        /// <summary>ROI�����'mouse button down'�¼��ķ�Ӧ:changing///��ROI����״��ӵ�ROIList(�������һ��'seed') </summary>
        /// <param name="imgX">x coordinate of mouse event</param>
        /// <param name="imgY">y coordinate of mouse event</param>
        /// <returns></returns>
        public string mouseDownAction(double imgX, double imgY)
        {//TODO:ROI�������
            string idxROI = "";
            double max = 10000, dist = 0;
            double epsilon = 15.0;          //maximal shortest distance to one of
                                            //the handles

            if (ROIMode != null)             //either a new ROI object is created
            {
                ROIMode.ImageWidth = viewController.ImageWidth;
                ROIMode.CreateROI(imgX, imgY);
                ROIList.Add(ROIName, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();
                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
            else if (ROIList.Count > 0)     // ... or an existing one is manipulated
            {
                ActiveROIId = "";
                foreach (KeyValuePair<string, ROI> kvp in ROIList)
                {
                    dist = kvp.Value.DistToClosestHandle(imgX, imgY);
                    if ((dist < max) && (dist < epsilon))
                    {
                        max = dist;
                        idxROI = kvp.Key;
                    }
                }
                if (idxROI.Length > 0)
                {
                    ActiveROIId = idxROI;
                    NotifyRCObserver(HWndCtrl.EVENT_ACTIVATED_ROI);
                }

                viewController.Repaint();
            }
            return ActiveROIId;
        }
        /// <summary>/// ROI�����'mouse button move'�¼��ķ�Ӧ:moving///�����ROI��</summary>
        /// <param name="newX">x coordinate of mouse event</param>
        /// <param name="newY">y coordinate of mouse event</param>
        public void mouseMoveAction(double newX, double newY)
        {
            try
            {
                if ((newX == currX) && (newY == currY))
                    return;

                ROIList[ActiveROIId].moveByHandle(newX, newY);
                viewController.Repaint();
                currX = newX;
                currY = newY;
                NotifyRCObserver(HWndCtrl.EVENT_MOVING_ROI);
            }
            catch (Exception)
            {
                //û����ʾroi��ʱ�� �ƶ����ᱨ��
            }

        }
        /***********************************************************/
        public void dummyI(int v)
        {
        }
        /*****************************/
        /// <summary>��ָ��λ����ʾROI--Rectangle1</summary>
        public void displayRect1(string name, string color, double row1, double col1, double row2, double col2)
        {
            SetROIShape(new ROIRectangle1());
            if (ROIMode != null)			 //either a new ROI object is created
            {
                ROIMode.CreateRectangle1(row1, col1, row2, col2);
                ROIMode.Type = ROIType.Rectangle1;
                ROIMode.Color = color;
                ROIList.Remove(name);
                ROIList.Add(name, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();

                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
        }
        /// <summary>
        /// ��ָ��λ����ʾROI--Rectangle2
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="phi"></param>
        /// <param name="length1"></param>
        /// <param name="length2"></param>
        /// <param name="rois"></param>
        public void displayRect2(string name, string color, double row, double col, double phi, double length1, double length2)
        {
            SetROIShape(new ROIRectangle2());

            if (ROIMode != null)			 //either a new ROI object is created
            {
                ROIMode.CreateRectangle2(row, col, phi, length1, length2);
                ROIMode.Type = ROIType.Rectangle2;
                ROIMode.Color = color;
                ROIList.Remove(name);
                ROIList.Add(name, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();

                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
        }
        /// <summary>
        /// ��ָ��λ������ROI--Circle
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="radius"></param>
        /// <param name="rois"></param>
        public void displayCircle(string name, string color, double row, double col, double radius)
        {
            SetROIShape(new ROICircle());

            if (ROIMode != null)			 //either a new ROI object is created
            {
                ROIMode.CreateCircle(row, col, radius);
                ROIMode.Type = ROIType.Circle;
                ROIMode.Color = color;
                ROIList.Remove(name);
                ROIList.Add(name, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();

                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
        }
        /// <summary> ��ָ��λ����ʾROI--Line 
        /// <param name="beginRow"></param>
        /// <param name="beginCol"></param>
        /// <param name="endRow"></param>
        /// <param name="endCol"></param>
        /// <param name="rois"></param>
        public void displayLine(string name, string color, double beginRow, double beginCol, double endRow, double endCol)
        {
            //this.SetROIShape(new ROILine());
            ROIMode = new ROILine();
            if (ROIMode != null)			 //either a new ROI object is created
            {
                ROIMode.CreateLine(beginRow, beginCol, endRow, endCol);
                ROIMode.Type = ROIType.Line;
                ROIMode.Color = color;
                ROIList.Remove(name);
                ROIList.Add(name, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();
                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
        }
        /// <summary> ��ָ��λ����ʾROI--Line 
        /// <param name="beginRow"></param>
        /// <param name="beginCol"></param>
        /// <param name="endRow"></param>
        /// <param name="endCol"></param>
        /// <param name="rois"></param>
        public void displayCoordLine(string name, string color, double beginRow, double beginCol, double endRow, double endCol)
        {
            //this.SetROIShape(new ROILine());
            ROIMode = new ROICoordLine();
            if (ROIMode != null)			 //either a new ROI object is created
            {
                ROIMode.CreateCoordLine(beginRow, beginCol, endRow, endCol);
                ROIMode.Type = ROIType.CoordLine;
                ROIMode.Color = color;
                ROIList.Remove(name);
                ROIList.Add(name, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();
                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
        }
        /// <summary>
        /// ��ָ��λ������ROI--Point
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="rois"></param>
        public void displayPoint(string name, string color, double row, double col)
        {
            SetROIShape(new ROIPoint());

            if (ROIMode != null)			 //either a new ROI object is created
            {
                ROIMode.CreatePoint(row, col);
                ROIMode.Type = ROIType.Point;
                ROIMode.Color = color;
                ROIList.Remove(name);
                ROIList.Add(name, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();
                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
        }



        /// <summary>
        /// ��ָ��λ������ROI--Rectangle1
        /// </summary>
        /// <param name="row1"></param>
        /// <param name="col1"></param>
        /// <param name="row2"></param>
        /// <param name="col2"></param>
        /// <param name="rois"></param>
        protected internal void genRect1(string name, double row1, double col1, double row2, double col2, ref Dictionary<string, ROI> rois)
        {
            SetROIShape(new ROIRectangle1());
            if (rois == null)
            {
                rois = new Dictionary<string, ROI>();
            }

            if (ROIMode != null)			 //either a new ROI object is created
            {
                ROIMode.CreateRectangle1(row1, col1, row2, col2);
                ROIMode.Type = ROIType.Rectangle1;
                rois.Remove(name);
                ROIList.Remove(name);
                rois.Add(name, ROIMode);
                ROIList.Add(name, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();
                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
        }
        /// <summary>
        /// ��ָ��λ������ROI--Rectangle2
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="phi"></param>
        /// <param name="length1"></param>
        /// <param name="length2"></param>
        /// <param name="rois"></param>
        protected internal void genRect2(string name, double row, double col, double phi, double length1, double length2, ref Dictionary<string, ROI> rois)
        {
            SetROIShape(new ROIRectangle2());

            if (rois == null)
            {
                rois = new Dictionary<string, ROI>();
            }

            if (ROIMode != null)			 //either a new ROI object is created
            {
                ROIMode.CreateRectangle2(row, col, phi, length1, length2);
                ROIMode.Type = ROIType.Rectangle2;
                rois.Remove(name);
                ROIList.Remove(name);
                rois.Add(name, ROIMode);
                ROIList.Add(name, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();

                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
        }
        /// <summary>
        /// ��ָ��λ������ROI--Circle
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="radius"></param>
        /// <param name="rois"></param>
        protected internal void genCircle(string name, double row, double col, double radius, ref Dictionary<string, ROI> rois)
        {
            SetROIShape(new ROICircle());
            if (rois == null)
            {
                rois = new Dictionary<string, ROI>();
            }
            if (ROIMode != null) //either a new ROI object is created
            {
                ROIMode.CreateCircle(row, col, radius);
                ROIMode.Type = ROIType.Circle;
                rois.Remove(name);
                ROIList.Remove(name);
                rois.Add(name, ROIMode);
                ROIList.Add(name, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();
                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
        }
        /// <summary>
        /// ��ָ��λ������ROI--Circle
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="radius"></param>
        /// <param name="rois"></param>
        protected internal void genCircleAre(string name, double row, double col, double radius, ref Dictionary<string, ROI> rois)
        {
            SetROIShape(new ROICircularArc());
            if (rois == null)
            {
                rois = new Dictionary<string, ROI>();
            }
            if (ROIMode != null) //either a new ROI object is created
            {
                ROIMode.CreateCircleAre(row, col, radius);
                ROIMode.Type = ROIType.CircleArc;
                rois.Remove(name);
                ROIList.Remove(name);
                rois.Add(name, ROIMode);
                ROIList.Add(name, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();
                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
        }
        /// <summary>
        /// ��ָ��λ������ROI--Line
        /// </summary>
        /// <param name="beginRow"></param>
        /// <param name="beginCol"></param>
        /// <param name="endRow"></param>
        /// <param name="endCol"></param>
        /// <param name="rois"></param>
        protected internal void genLine(string name, double beginRow, double beginCol, double endRow, double endCol, ref Dictionary<string, ROI> rois)
        {
            this.SetROIShape(new ROILine());

            if (rois == null)
            {
                rois = new Dictionary<string, ROI>();
            }

            if (ROIMode != null)			 //either a new ROI object is created
            {
                ROIMode.CreateLine(beginRow, beginCol, endRow, endCol);
                ROIMode.Type = ROIType.Line;
                rois.Remove(name);
                ROIList.Remove(name);
                rois.Add(name, ROIMode);
                ROIList.Add(name, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();

                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
        }
        /// <summary>
        /// ��ָ��λ������ROI--CoordLine
        /// </summary>
        /// <param name="beginRow"></param>
        /// <param name="beginCol"></param>
        /// <param name="endRow"></param>
        /// <param name="endCol"></param>
        /// <param name="rois"></param>
        protected internal void genCoordLine(string name, double beginRow, double beginCol, double endRow, double endCol, ref Dictionary<string, ROI> rois)
        {
            this.SetROIShape(new ROICoordLine());

            if (rois == null)
            {
                rois = new Dictionary<string, ROI>();
            }

            if (ROIMode != null)			 //either a new ROI object is created
            {
                ROIMode.CreateCoordLine(beginRow, beginCol, endRow, endCol);
                ROIMode.Type = ROIType.CoordLine;
                rois.Remove(name);
                ROIList.Remove(name);
                rois.Add(name, ROIMode);
                ROIList.Add(name, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();

                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
        }


        /// <summary>
        /// ��ָ��λ������ROI--Point
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="radius"></param>
        /// <param name="rois"></param>
        protected internal void genPoint(string name, double row, double col, ref Dictionary<string, ROI> rois)
        {
            SetROIShape(new ROIPoint());
            if (rois == null)
            {
                rois = new Dictionary<string, ROI>();
            }
            if (ROIMode != null) //either a new ROI object is created
            {
                ROIMode.CreatePoint(row, col);
                ROIMode.Type = ROIType.Point;
                rois.Remove(name);
                ROIList.Remove(name);
                rois.Add(name, ROIMode);
                ROIList.Add(name, ROIMode);
                ROIMode = null;
                ActiveROIId = "";
                viewController.Repaint();
                NotifyRCObserver(HWndCtrl.EVENT_CREATED_ROI);
            }
        }
        /// <summary>
        /// ��ȡ��ǰѡ��ROI����Ϣ
        /// </summary>
        /// <returns></returns>
        protected internal ROI smallestActiveROI(out string name, out string index)
        {
            name = "";
            string activeROIIdx = this.GetActiveROIId();
            index = activeROIIdx;
            if (activeROIIdx.Length > 0)
            {
                ROI region = this.GetActiveROI();
                name = region.GetType().ToString();
                switch (name.Split('.')[1])
                {
                    case "ROIPoint":
                        region.Type = ROIType.Point;
                        break;
                    case "ROILine":
                        region.Type = ROIType.Line;
                        break;
                    case "ROICoordLine":
                        region.Type = ROIType.CoordLine;
                        break;
                    case "ROICircle":
                        region.Type = ROIType.Circle;
                        break;
                    case "ROIRectangle1":
                        region.Type = ROIType.Rectangle1;
                        break;
                    case "ROIRectangle2":
                        region.Type = ROIType.Rectangle2;
                        break;
                }
                return region;
            }
            else
            {
                return null;
            }
        }
        protected internal ROI smallestActiveROI(out List<double> data, out string index)
        {
            try
            {
                index = this.GetActiveROIId();
                data = new List<double>();
                if (index.Length > 0)
                {
                    ROI region = this.GetActiveROI();
                    Type type = region.GetType();
                    HTuple smallest = region.GetModelData();
                    for (int i = 0; i < smallest.Length; i++)
                    {
                        data.Add(smallest[i].D);
                    }
                    return region;
                }
                else
                {
                    return null;
                }

            }
            catch (Exception)
            {
                data = null;
                index = "";
                return null;
            }
        }
        /// <summary>ɾ����ǰѡ��ROI </summary>
        /// <param name="roi"></param>
        protected internal void removeActiveROI(ref Dictionary<string, ROI> roi)
        {
            try
            {
                string activeROIIdx = this.GetActiveROIId();
                if (activeROIIdx.Length > 0)
                {
                    this.RemoveActive();
                    roi.Remove(activeROIIdx);
                }
            }
            catch { }
        }
        /// <summary>ѡ�м���ROI</summary>
        /// <param name="index"></param>
        protected internal void selectROI(string index)
        {
            this.ActiveROIId = index;
            this.NotifyRCObserver(HWndCtrl.EVENT_ACTIVATED_ROI);
            this.viewController.Repaint();
        }





        /// <summary>
        /// ��������
        /// </summary>
        /// <param name="Row"></param>
        /// <param name="Column"></param>
        /// <param name="zoomWndFactor"></param>
        public HRegion Eraser(double Row, double Column, double zoomWndFactor)
        {
            BrushRegion = new HRegion();
            HRegion tmpDiff = new HRegion();
            tmpDiff.GenEmptyRegion();
            if (10 * zoomWndFactor < 1)
            {
                BrushRegion.GenCircle(Row, Column, 0.5);
            }
            else
            {
                BrushRegion.GenCircle(Row, Column, 10 * zoomWndFactor);
            }
            if (Row >= 0 && Column >= 0)
            {
                if (MaskRegion == null)
                {
                    MaskRegion = new HRegion();
                    MaskRegion = tmpDiff.Difference(BrushRegion);
                }

                else
                    MaskRegion = MaskRegion.Difference(BrushRegion);
                return BrushRegion;
            }
            else
            {
                return BrushRegion;
            }
        }
        /// <summary>
        ///  ��Ϳ����
        /// </summary>
        /// <param name="Row"></param>
        /// <param name="Column"></param>
        /// <param name="zoomWndFactor"></param>
        public HRegion Paint(double Row, double Column, double zoomWndFactor)
        {
            BrushRegion = new HRegion();
            HRegion tmpAdd = new HRegion();
            tmpAdd.GenEmptyRegion();
            if (10 * zoomWndFactor < 1)
            {
                BrushRegion.GenCircle(Row, Column, 0.5);
            }
            else
            {
                BrushRegion.GenCircle(Row, Column, 10 * zoomWndFactor);
            }
            if (Row >= 0 && Column >= 0)
            {
                if (MaskRegion == null)
                {
                    MaskRegion = new HRegion();
                    MaskRegion = tmpAdd.Union2(BrushRegion);
                }

                else
                    MaskRegion = MaskRegion.Union2(BrushRegion);
                return BrushRegion;
            }
            else
            {
                return BrushRegion;
            }
        }
        /// <summary>��λ������ʾ</summary>
        protected internal void ResetWindowImage()
        {
            //this.viewController.ResetWindow();
            this.viewController.Repaint();
        }
        protected internal void zoomWindowImage()
        {
            this.viewController.SetViewState(HWndCtrl.MODE_VIEW_ZOOM);
        }
        protected internal void moveWindowImage()
        {
            this.viewController.SetViewState(HWndCtrl.MODE_VIEW_MOVE);
        }
        protected internal void noneWindowImage()
        {
            this.viewController.SetViewState(HWndCtrl.MODE_VIEW_NONE);
        }
    }//end of class
}//end of namespace
