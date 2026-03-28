using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;
using Xunit;

namespace TiaPortalMcpServer.Tests
{
    [Trait("Category", "Integration")]
    public class ProjectManagementToolsTests : TestBase
    {
        private readonly ProjectManagementTools _projectTools;

        public ProjectManagementToolsTests()
        {
            _projectTools = ServiceProvider.GetRequiredService<ProjectManagementTools>();
        }

        [Fact]
        public async Task CreateProject_ValidName_ReturnsSuccess()
        {
            // Act
            var result = await _projectTools.projects_create(null, "TestProject", "C:\\Temp", CancellationToken.None);

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.True(response.Success);
        }

        [Fact]
        public async Task OpenProject_ValidPath_ReturnsSuccess()
        {
            // This would require a valid project file path
            // For now, test with invalid path to check error handling
            var result = await _projectTools.projects_open(null, "InvalidPath", CancellationToken.None);

            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            // Should fail with invalid path
            Assert.False(response.Success);
        }

        [Fact]
        public void SaveProject_NoProject_ReturnsError()
        {
            // Arrange - ensure no project
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = _projectTools.projects_save();

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }

        [Fact]
        public void CloseProject_NoProject_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = _projectTools.projects_close();

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }

        [Fact]
        public void GetSessionInfo_ReturnsInfo()
        {
            // Act
            var result = _projectTools.projects_get_session_info();

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
        }
    }
}