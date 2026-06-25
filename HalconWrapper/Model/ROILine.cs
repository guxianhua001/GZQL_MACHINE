using System;
using HalconDotNet;
using System.Xml.Serialization;

namespace HalconWrapper.Model
{
	/// <summary>
	/// This class demonstrates one of the possible implementations for a 
	/// linear ROI. ROILine inherits from the base class ROI and 
	/// implements (besides other auxiliary methods) all virtual methods 
	/// defined in ROI.cs.
	/// </summary>
    [Serializable]
    public class ROILine : ROI
    {
        public bool Status;
        public double _StartY;
        /// <summary>���������</summary>
        public double StartY
        {
            get { return _StartY; }
            set { Set(ref _StartY, value); }
        }
        public double _StartX;
        /// <summarX>���������</summarX>
        public double StartX
        {
            get { return _StartX; }
            set { Set(ref _StartX, value); }
        }
        public double _EndY;
        /// <summary>�յ�������</summary>
        public double EndY
        {
            get { return _EndY; }
            set { Set(ref _EndY, value); }
        }
        public double _EndX;
        /// <summary>�յ�������</summary>
        public double EndX
        {
            get { return _EndX; }
            set { Set(ref _EndX, value); }
        }
        public double _MidX;
        /// <summary>�е�������</summary>
        public double MidX
        {
            get { return _MidX; }
            set { Set(ref _MidX, value); }
        }

        public double _MidY;
        /// <summary>�е�������</summary>
        public double MidY
        {
            get { return _MidY; }
            set { Set(ref _MidY, value); }
        }
        public double _Phi;
        /// <summary>ֱ�߽Ƕ�</summary>
        public double Phi
        {
            get { return _Phi; }
            set { Set(ref _Phi, value); }
        }
        /// <summary>ֱ�߳���</summary>
        public double Dist;
        /// <summary>������</summary>
        public double Nx;
        /// <summary>������</summary>
        public double Ny;
        /// <summary>X�㼯��</summary>
        public double[] X;
        /// <summary>Y�㼯��</summary>
        public double[] Y;
        /// <summary>ֱ������Ӽ�ͷ��ʾ</summary>
        private HObject arrowHandleXLD;
        public ROILine()
        {

            NumHandles = 3;        // two end points of line
            ActiveHandleId = 2;
            Status = true;
        }

        public ROILine(double beginRow, double beginCol, double endRow, double endCol)
        {
            CreateLine(beginRow, beginCol, endRow, endCol);
        }

        public override void CreateLine(double beginRow, double beginCol, double endRow, double endCol)
        {
            base.CreateLine(beginRow, beginCol, endRow, endCol);

            StartY = beginRow;
            StartX = beginCol;
            EndY = endRow;
            EndX = endCol;
            Ny = StartY - EndY;
            Nx = EndX - StartX;
            MidX = (StartY + EndY) / 2.0;
            MidY = (StartX + EndX) / 2.0;
            Phi = HMisc.AngleLx(StartX, StartY, EndX, EndY);
            ShowArrowHandle();
            Status = true;
        }

        /// <summary>Creates a new ROI instance at the mouse position.</summary>
        public override void CreateROI(double midX, double midY)
        {
            MidX = midY;
            MidY = midX;

            StartY = MidX;
            StartX = MidY - 50;
            EndY = MidX;
            EndX = MidY + 50;

            Ny = StartY - EndY;
            Nx = EndX - StartX;

            ShowArrowHandle();
        }
        /// <summary>Paints the ROI into the supplied window.</summary>
        public override void Draw(HWindow window)
        {
            // 根据当前缩放计算自适应手柄大小，放大时变小
            double handleSize = GetZoomAwareHandleSize(window);
            window.SetDraw("margin");
            //window.DispArrow(StartY, StartX, EndY, EndX,3);
            window.DispLine(StartY, StartX, EndY, EndX);
            window.SetDraw("fill");
            window.DispRectangle2(StartY, StartX, 0, handleSize, handleSize);
            window.DispObj(arrowHandleXLD);  //window.DispRectangle2( EndY, EndX, 0, 25, 25);
            window.DispRectangle2(MidX, MidY, 0, handleSize, handleSize);
            Phi = HMisc.AngleLx(StartY, StartX, EndY, EndX);
        }

        /// <summary> 
        /// Returns the distance of the ROI handle being
        /// closest to the image point(x,y).
        /// </summary>
        public override double DistToClosestHandle(double x, double y)
        {

            double max = 10000;
            double[] val = new double[NumHandles];

            val[0] = HMisc.DistancePp(y, x, StartY, StartX); // upper left 
            val[1] = HMisc.DistancePp(y, x, EndY, EndX); // upper right 
            val[2] = HMisc.DistancePp(y, x, MidX, MidY); // midpoint 

            for (int i = 0; i < NumHandles; i++)
            {
                if (val[i] < max)
                {
                    max = val[i];
                    ActiveHandleId = i;
                }
            }// end of for 

            return val[ActiveHandleId];
        }

        /// <summary> 
        /// Paints the active handle of the ROI object into the supplied window. 
        /// </summary>
        public override void DisplayActive(HWindow window)
        {
            // 根据当前缩放计算自适应手柄大小，放大时变小
            double handleSize = GetZoomAwareHandleSize(window);
            switch (ActiveHandleId)
            {
                case 0:
                    window.DispRectangle2(StartY, StartX, 0, handleSize, handleSize);
                    break;
                case 1:
                    window.DispObj(arrowHandleXLD); //window.DispRectangle2(EndY, EndX, 0, 25, 25);
                    break;
                case 2:
                    window.DispRectangle2(MidX, MidY, 0, handleSize, handleSize);
                    break;
            }
        }

        /// <summary>Gets the HALCON region described by the ROI.</summary>
        public override HRegion GetRegion()
        {
            HRegion region = new HRegion();
            region.GenRegionLine(StartY, StartX, EndY, EndX);
            return region;
        }
        public override HXLDCont GetXLD()
        {
            HXLDCont xld = null;
            try
            {
                HOperatorSet.GenContourPolygonXld(out HObject m_ResultXLD, new HTuple(StartX, EndX), new HTuple(StartY, EndY));
                xld = new HXLDCont(m_ResultXLD);
                return xld;
            }
            catch { }
            return xld;
        }
        public override double GetDistanceFromStartPoint(double row, double col)
        {
            double distance = HMisc.DistancePp(row, col, StartY, StartX);
            return distance;
        }
        /// <summary>
        /// Gets the model information described by 
        /// the ROI.
        /// </summary> 
        public override HTuple GetModelData()
        {
            return new HTuple(new double[] { StartY, StartX, EndY, EndX });
        }

        /// <summary> 
        /// Recalculates the shape of the ROI. Translation is 
        /// performed at the active handle of the ROI object 
        /// for the image coordinate (x,y).
        /// </summary>
        public override void moveByHandle(double newX, double newY)
        {
            double lenR, lenC;

            switch (ActiveHandleId)
            {
                case 0: // first end point
                    StartY = newY;
                    StartX = newX;

                    MidX = (StartY + EndY) / 2;
                    MidY = (StartX + EndX) / 2;
                    break;
                case 1: // last end point
                    EndY = newY;
                    EndX = newX;

                    MidX = (StartY + EndY) / 2;
                    MidY = (StartX + EndX) / 2;
                    break;
                case 2: // midpoint 
                    lenR = StartY - MidX;
                    lenC = StartX - MidY;

                    MidX = newY;
                    MidY = newX;

                    StartY = MidX + lenR;
                    StartX = MidY + lenC;
                    EndY = MidX - lenR;
                    EndX = MidY - lenC;
                    break;
            }
            ShowArrowHandle();
        }

        /// <summary>
        /// Auxiliary method
        /// </summary>
        private void ShowArrowHandle()
        {
            double length, dr, dc, halfHW;
            double rrow1, ccol1, rowP1, colP1, rowP2, colP2;

            double headLength = 15;
            double headWidth = 15;
            arrowHandleXLD = new HXLDCont();
            arrowHandleXLD.GenEmptyObj();

            arrowHandleXLD.Dispose();
            arrowHandleXLD.GenEmptyObj();

            rrow1 = StartY + (EndY - StartY) * 0.9;
            ccol1 = StartX + (EndX - StartX) * 0.9;

            length = HMisc.DistancePp(rrow1, ccol1, EndY, EndX);
            if (length == 0)
                length = -1;

            dr = (EndY - rrow1) / length;
            dc = (EndX - ccol1) / length;

            halfHW = headWidth / 2;
            rowP1 = (rrow1 + (length - headLength) * dr + halfHW * dc);
            colP1 = (ccol1 + (length - headLength) * dc - halfHW * dr);
            rowP2 = (rrow1 + (length - headLength) * dr - halfHW * dc);
            colP2 = (ccol1 + (length - headLength) * dc + halfHW * dr);
            Phi = HMisc.AngleLx(StartY, StartX, EndY, EndX);
            if (length == -1)
                HOperatorSet.GenContourPolygonXld(out arrowHandleXLD, rrow1, ccol1);
            else
                HOperatorSet.GenContourPolygonXld(out arrowHandleXLD, new HTuple(new double[] { rrow1, EndY, rowP1, EndY, rowP2, EndY }),
                                                    new HTuple(new double[] { ccol1, EndX, colP1, EndX, colP2, EndX }));
        }
        public object Clone()
        {
            return this.MemberwiseClone();
        }

    }//end of class
}//end of namespace
