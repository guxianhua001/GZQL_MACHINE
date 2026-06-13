using Core.Abstraction;
using Core.Models;
using System.Collections.Generic;

namespace Core.Services
{
    public class ZScanArcCompensationService : IZScanArcCompensationService
    {
        /// <summary>
        /// 将相机测量值按行顺序映射到点数据表格：
        /// 第 i 行 = arcHeights[i] + totalOffset
        /// Z-SCAN 3D相机每次返回数组数据，按行顺序一一对应。
        /// </summary>
        public void Compensate(List<ZScanPointData> points, double[] arcHeights, double totalOffset, ZScanDataFormat dataFormat)
        {
            if (points == null || points.Count == 0 || arcHeights == null || arcHeights.Length == 0)
                return;

            for (int i = 0; i < points.Count; i++)
            {
                if (i >= arcHeights.Length)
                    continue;

                var point = points[i];
                double measuredValue = arcHeights[i] + totalOffset;

                point.ZMeasured = measuredValue;
                point.DeltaZ = measuredValue - point.Nominal;
            }
        }
    }
}
