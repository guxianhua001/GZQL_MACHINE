using Core.Models;
using Core.Services;
using System.Collections.Generic;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanArcCompensationServiceTests
    {
        [Fact]
        public void Compensate_SinglePoint_ReturnsDeltaZ()
        {
            var service = new ZScanArcCompensationService();
            var points = new List<ZScanPointData>
            {
                new ZScanPointData { Nominal = 5.000, DataIndex = 0 }
            };
            double[] arcHeights = { 5.012 };
            double totalOffset = 0.0;

            service.Compensate(points, arcHeights, totalOffset, ZScanDataFormat.Double);

            Assert.Equal(5.012, points[0].ZMeasured);
            Assert.Equal(0.012, points[0].DeltaZ, 3);
        }

        [Fact]
        public void Compensate_ArcPoints_MapsByDataIndex()
        {
            var service = new ZScanArcCompensationService();
            var points = new List<ZScanPointData>
            {
                new ZScanPointData { Nominal = 5.000, DataIndex = 0 },
                new ZScanPointData { Nominal = 5.010, DataIndex = 1 },
                new ZScanPointData { Nominal = 5.020, DataIndex = 2 }
            };
            double[] arcHeights = { 5.012, 5.025, 5.051 };
            double totalOffset = 0.0;

            service.Compensate(points, arcHeights, totalOffset, ZScanDataFormat.DoubleArray);

            Assert.Equal(5.012, points[0].ZMeasured, 3);
            Assert.Equal(0.012, points[0].DeltaZ, 3);
            Assert.Equal(5.025, points[1].ZMeasured, 3);
            Assert.Equal(0.015, points[1].DeltaZ, 3);
            Assert.Equal(5.051, points[2].ZMeasured);
            Assert.Equal(0.031, points[2].DeltaZ, 3);
        }

        [Fact]
        public void Compensate_ArcPoints_AllDeltasCalculated()
        {
            var service = new ZScanArcCompensationService();
            var points = new List<ZScanPointData>
            {
                new ZScanPointData { Nominal = 3.500, DataIndex = 0 },
                new ZScanPointData { Nominal = 3.500, DataIndex = 1 }
            };
            double[] arcHeights = { 3.480, 3.520 };
            double totalOffset = 0.0;

            service.Compensate(points, arcHeights, totalOffset, ZScanDataFormat.DoubleArray);

            Assert.Equal(-0.020, points[0].DeltaZ, 3);
            Assert.Equal(0.020, points[1].DeltaZ, 3);
        }

        [Fact]
        public void Compensate_WithCalibrationOffset_AppliedCorrectly()
        {
            var service = new ZScanArcCompensationService();
            var points = new List<ZScanPointData>
            {
                new ZScanPointData { Nominal = 5.000, DataIndex = 0 }
            };
            double[] arcHeights = { 5.012 };
            double totalOffset = 0.5;

            service.Compensate(points, arcHeights, totalOffset, ZScanDataFormat.Double);

            Assert.Equal(5.512, points[0].ZMeasured);
            Assert.Equal(0.512, points[0].DeltaZ, 3);
        }

        [Fact]
        public void Compensate_MoreMeasuredPointsThanTableRows_OnlyMapsExisting()
        {
            var service = new ZScanArcCompensationService();
            var points = new List<ZScanPointData>
            {
                new ZScanPointData { Nominal = 5.000, DataIndex = 0 },
                new ZScanPointData { Nominal = 5.010, DataIndex = 1 }
            };
            double[] arcHeights = { 5.012, 5.025, 5.051, 5.049 };
            double totalOffset = 0.0;

            service.Compensate(points, arcHeights, totalOffset, ZScanDataFormat.DoubleArray);

            Assert.Equal(2, points.Count);
            Assert.Equal(5.012, points[0].ZMeasured);
            Assert.Equal(5.025, points[1].ZMeasured);
        }

        [Fact]
        public void Compensate_EmptyPoints_NoCrash()
        {
            var service = new ZScanArcCompensationService();
            var points = new List<ZScanPointData>();
            double[] arcHeights = { 5.012 };

            service.Compensate(points, arcHeights, 0.0, ZScanDataFormat.DoubleArray);

            Assert.Empty(points);
        }

        [Fact]
        public void Compensate_EmptyArcHeights_NoChange()
        {
            var service = new ZScanArcCompensationService();
            var points = new List<ZScanPointData>
            {
                new ZScanPointData { Nominal = 5.000, ZMeasured = 0, DataIndex = 0 }
            };
            double[] arcHeights = System.Array.Empty<double>();

            service.Compensate(points, arcHeights, 0.0, ZScanDataFormat.DoubleArray);

            Assert.Equal(0, points[0].ZMeasured);
        }

        [Fact]
        public void Compensate_DoubleArrayWithOffset_AllCorrect()
        {
            var service = new ZScanArcCompensationService();
            var points = new List<ZScanPointData>
            {
                new ZScanPointData { Nominal = 5.000, DataIndex = 0 },
                new ZScanPointData { Nominal = 5.010, DataIndex = 1 }
            };
            double[] arcHeights = { 4.812, 4.825 };
            double totalOffset = 0.2;

            service.Compensate(points, arcHeights, totalOffset, ZScanDataFormat.DoubleArray);

            Assert.Equal(5.012, points[0].ZMeasured, 3);
            Assert.Equal(0.012, points[0].DeltaZ, 3);
            Assert.Equal(5.025, points[1].ZMeasured, 3);
            Assert.Equal(0.015, points[1].DeltaZ, 3);
        }

        [Fact]
        public void Compensate_MoreRowsThanData_SkipsExtraRows()
        {
            var service = new ZScanArcCompensationService();
            var points = new List<ZScanPointData>
            {
                new ZScanPointData { Nominal = 5.000, DataIndex = 0 },
                new ZScanPointData { Nominal = 5.010, DataIndex = 5 }
            };
            double[] arcHeights = { 5.012 };
            double totalOffset = 0.0;

            service.Compensate(points, arcHeights, totalOffset, ZScanDataFormat.DoubleArray);

            // 第1行有对应数据，第2行超出数据范围被跳过
            Assert.Equal(5.012, points[0].ZMeasured);
            Assert.Equal(0, points[1].ZMeasured);
        }
    }
}
