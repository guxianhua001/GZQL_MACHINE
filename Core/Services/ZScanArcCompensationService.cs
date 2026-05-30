using Core.Abstraction;
using Core.Models;
using System.Collections.Generic;

namespace Core.Services
{
    public class ZScanArcCompensationService : IZScanArcCompensationService
    {
        public void Compensate(List<ZScanPointData> points, double[] arcHeights, double totalOffset, ZScanDataFormat dataFormat)
        {
            if (points == null || points.Count == 0 || arcHeights == null || arcHeights.Length == 0)
                return;

            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                double measuredValue;

                if (dataFormat == ZScanDataFormat.Double)
                {
                    measuredValue = arcHeights[0] + totalOffset;
                }
                else
                {
                    int dataIndex = point.DataIndex;
                    if (dataIndex < 0 || dataIndex >= arcHeights.Length)
                        continue;

                    measuredValue = arcHeights[dataIndex] + totalOffset;
                }

                point.ZMeasured = measuredValue;
                point.DeltaZ = measuredValue - point.Nominal;
            }
        }
    }
}
