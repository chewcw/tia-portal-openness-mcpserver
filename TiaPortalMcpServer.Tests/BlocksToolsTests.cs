using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;
using Xunit;

namespace TiaPortalMcpServer.Tests
{
    public class BlocksToolsTests : TestBase
    {
        private readonly BlocksTools _blocksTools;

        public BlocksToolsTests()
        {
            _blocksTools = ServiceProvider.GetRequiredService<BlocksTools>();
        }

        [Fact]
        public void GetAssignedProDiagFb_NoProject_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = _blocksTools.blocks_prodiag_assigned_get("TestDevice", "TestMember", "db");

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }

        [Fact]
        public void SetAssignedProDiagFb_NoProject_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = _blocksTools.blocks_prodiag_assigned_set("TestDevice", "TestMember", "db", "TestFb");

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }

        [Fact]
        public void ListSystemDataTypes_NoProject_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = _blocksTools.blocks_system_types_list("TestDevice");

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }

        // Add more tests as needed for other methods
    }
}