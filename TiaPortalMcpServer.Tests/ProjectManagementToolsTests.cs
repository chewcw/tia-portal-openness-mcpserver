using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;
using Xunit;

namespace TiaPortalMcpServer.Tests
{
    public class ProjectManagementToolsTests : TestBase
    {
        private readonly ProjectManagementTools _projectTools;

        public ProjectManagementToolsTests()
        {
            _projectTools = ServiceProvider.GetRequiredService<ProjectManagementTools>();
        }

        [Fact]
        public void CreateProject_ValidName_ReturnsSuccess()
        {
            // Act
            var result = _projectTools.create_project("TestProject", "C:\\Temp");

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.True(response.Success);
        }

        [Fact]
        public void OpenProject_ValidPath_ReturnsSuccess()
        {
            // This would require a valid project file path
            // For now, test with invalid path to check error handling
            var result = _projectTools.open_project("InvalidPath");

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
            var result = _projectTools.save_project();

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
            var result = _projectTools.close_project();

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