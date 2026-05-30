using Core.Models;
using Module.Services;
using Moq;
using Recipe.Interfaces;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanGlobalVariableLinkServiceTests
    {
        private ZScanGlobalVariableLinkService CreateService(List<GlobalVariable> variables)
        {
            var mockRecipePool = new Mock<IRecipePoolService>();
            mockRecipePool.Setup(r => r.CurrentPoolName).Returns("TestPool");
            mockRecipePool.Setup(r => r.LoadGlobalVariablesAsync(It.IsAny<string>()))
                .ReturnsAsync(variables);
            mockRecipePool.Setup(r => r.SaveGlobalVariablesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<GlobalVariable>>()))
                .Callback<string, IEnumerable<GlobalVariable>>((_, _) => { });

            var mockEventAggregator = new Mock<Prism.Events.IEventAggregator>();

            return new ZScanGlobalVariableLinkService(mockRecipePool.Object, mockEventAggregator.Object);
        }

        [Fact]
        public void LinkVariable_ValidName_SetsIsLinked()
        {
            var variables = new List<GlobalVariable>
            {
                new GlobalVariable { Name = "Z_Height", Type = GlobalVariableType.Double, Value = "5.0" }
            };
            var service = CreateService(variables);

            bool result = service.LinkVariable("Z_Height", GlobalVariableType.Double);

            Assert.True(result);
            Assert.True(service.IsLinked);
            Assert.Equal("Z_Height", service.LinkedVariableName);
        }

        [Fact]
        public void LinkVariable_InvalidName_ReturnsFalse()
        {
            var variables = new List<GlobalVariable>();
            var service = CreateService(variables);

            bool result = service.LinkVariable("NonExistent", GlobalVariableType.Double);

            Assert.False(result);
            Assert.False(service.IsLinked);
        }

        [Fact]
        public void UnlinkVariable_SetsIsLinkedFalse()
        {
            var variables = new List<GlobalVariable>
            {
                new GlobalVariable { Name = "Z_Height", Type = GlobalVariableType.Double, Value = "5.0" }
            };
            var service = CreateService(variables);
            service.LinkVariable("Z_Height", GlobalVariableType.Double);

            service.UnlinkVariable();

            Assert.False(service.IsLinked);
            Assert.Equal(string.Empty, service.LinkedVariableName);
        }

        [Fact]
        public void GetLinkedValue_DoubleType_ReturnsCorrectValue()
        {
            var variables = new List<GlobalVariable>
            {
                new GlobalVariable { Name = "Z_Height", Type = GlobalVariableType.Double, Value = "5.123" }
            };
            var service = CreateService(variables);
            service.LinkVariable("Z_Height", GlobalVariableType.Double);

            var value = service.GetLinkedValue();

            Assert.NotNull(value);
            Assert.Equal(5.123, (double)value, 3);
        }

        [Fact]
        public void GetLinkedValue_NotLinked_ReturnsNull()
        {
            var variables = new List<GlobalVariable>();
            var service = CreateService(variables);

            var value = service.GetLinkedValue();

            Assert.Null(value);
        }

        [Fact]
        public void DefaultState_NotLinked()
        {
            var variables = new List<GlobalVariable>();
            var service = CreateService(variables);

            Assert.False(service.IsLinked);
            Assert.Equal(string.Empty, service.LinkedVariableName);
        }
    }
}
