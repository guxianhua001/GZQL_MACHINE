#if !HAS_HALCON
using System.Collections.Generic;
using Core.Models;

namespace Core.Services
{
    /// <summary>
    /// 坐标对齐服务空实现——Halcon SDK 未安装时代替 CoordinateAlignService，
    /// 避免 DI 容器解析失败。所有方法为空操作或返回默认值。
    /// </summary>
    public class StubCoordinateAlignService : ICoordinateAlignService
    {
        public AlignMode CurrentMode => AlignMode.Affine;

        public void SetMode(AlignMode mode) { }
        public void SetMapFiducial(double x, double y, double z) { }
        public void SetMachineFiducial(double x, double y, double z, double rx, double rz) { }
        public void AutoCalculate() { }
        public void RegisterPoints(IEnumerable<CadPoint> cadPoints) { }
        public void SetPointMapping(string pointId, double mx, double my, double mz) { }
        public CadPoint TransformToMachine(CadPoint cadPoint) => cadPoint;
        public CoordinateTransform GetTransform() => new CoordinateTransform();
        public void SetDirectionLength(double length) { }
        public void AutoCalculateAffine() { }
        public string GetAffineMatrixDisplay() => string.Empty;
    }
}
#endif
