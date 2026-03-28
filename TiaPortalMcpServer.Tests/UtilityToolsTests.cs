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
    public class UtilityToolsTests : TestBase
    {
        private readonly UtilityTools _utilityTools;

        public UtilityToolsTests()
        {
            _utilityTools = ServiceProvider.GetRequiredService<UtilityTools>();
        }

        [Fact]
        public void GetProjectMetadata_NoProject_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = _utilityTools.utilities_get_project_info();

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }

        [Fact]
        public void ListLibraries_ReturnsSuccess()
        {
            // Act
            var result = _utilityTools.utilities_list_libraries();

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
        }
    }
}