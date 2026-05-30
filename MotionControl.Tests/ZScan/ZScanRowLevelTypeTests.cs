using Core.Models;
using Module.Models;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanRowLevelTypeTests
    {
        [Fact]
        public void ZScanPointData_DefaultPointType_IsDouble()
        {
            var point = new ZScanPointData();
            Assert.Equal(ZScanDataFormat.Double, point.PointType);
        }

        [Fact]
        public void ZScanPointData_GlobalVariableLink_DefaultIsNull()
        {
            var point = new ZScanPointData();
            Assert.Null(point.GlobalVariableLink);
        }

        [Fact]
        public void ZScanPointData_SetPointType_DoubleArray()
        {
            var point = new ZScanPointData { PointType = ZScanDataFormat.DoubleArray };
            Assert.Equal(ZScanDataFormat.DoubleArray, point.PointType);
        }

        [Fact]
        public void ZScanPointData_SetGlobalVariableLink()
        {
            var link = new ZScanGlobalVariableLink { IsLinked = true, VariableName = "GV_ArcHeight", VariableType = GlobalVariableType.DoubleArray };
            var point = new ZScanPointData { GlobalVariableLink = link };
            Assert.True(point.GlobalVariableLink.IsLinked);
            Assert.Equal("GV_ArcHeight", point.GlobalVariableLink.VariableName);
            Assert.Equal(GlobalVariableType.DoubleArray, point.GlobalVariableLink.VariableType);
        }

        [Fact]
        public void ZScanPointDetail_PointType_DefaultIsDouble()
        {
            var detail = new ZScanPointDetail();
            Assert.Equal(ZScanDataFormat.Double, detail.PointType);
        }

        [Fact]
        public void ZScanPointDetail_GlobalVariableLink_DefaultIsNull()
        {
            var detail = new ZScanPointDetail();
            Assert.Null(detail.GlobalVariableLink);
        }

        [Fact]
        public void ZScanPointDetail_SetPointType_RaisesPropertyChanged()
        {
            var detail = new ZScanPointDetail();
            bool raised = false;
            detail.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(ZScanPointDetail.PointType)) raised = true; };
            detail.PointType = ZScanDataFormat.DoubleArray;
            Assert.True(raised);
        }

        [Fact]
        public void ZScanPointDetail_SetGlobalVariableLink_UpdatesIsGlobalVarLinked()
        {
            var detail = new ZScanPointDetail();
            Assert.False(detail.IsGlobalVarLinked);
            detail.GlobalVariableLink = new ZScanGlobalVariableLink { IsLinked = true, VariableName = "GV_Test" };
            Assert.True(detail.IsGlobalVarLinked);
        }

        [Fact]
        public void ZScanPointDetail_UnlinkGlobalVariable_SetsIsGlobalVarLinkedFalse()
        {
            var detail = new ZScanPointDetail
            {
                GlobalVariableLink = new ZScanGlobalVariableLink { IsLinked = true, VariableName = "GV_Test" }
            };
            detail.GlobalVariableLink = null;
            Assert.False(detail.IsGlobalVarLinked);
        }
    }
}
