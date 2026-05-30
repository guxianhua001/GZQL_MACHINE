using Core.Models;
using System.Collections.Generic;

namespace Core.Abstraction
{
    public interface IZScanArcCompensationService
    {
        void Compensate(List<ZScanPointData> points, double[] arcHeights, double totalOffset, ZScanDataFormat dataFormat);
    }
}
