using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;
using Xunit;

namespace TiaPortalMcpServer.Tests
{
    [Trait("Category", "Integration")]
    public class SoftwareToolsTests : TestBase
    {
        private readonly SoftwareTools _softwareTools;

        public SoftwareToolsTests()
        {
            _softwareTools = ServiceProvider.GetRequiredService<SoftwareTools>();
        }

        [Fact]
        public void AddBlock_NoProject_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = _softwareTools.software_add_block("TestDevice", "FC", "TestBlock");

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }

        [Fact]
        public void ListBlocks_NoProject_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = _softwareTools.blocks_list("TestDevice");

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }

        [Fact]
        public void GetBlockHierarchy_NoProject_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = _softwareTools.software_get_block_hierarchy("TestDevice");

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }

        [Fact]
        public void AddTag_NoProject_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = _softwareTools.software_add_block("TestDevice", "FB", "TestBlock");

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }
    }
}