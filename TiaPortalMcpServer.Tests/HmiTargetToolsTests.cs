using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;
using Xunit;

namespace TiaPortalMcpServer.Tests
{
    /// <summary>
    /// Unit tests for HmiTargetTools MCP server tool class.
    /// Tests cover error handling, project validation, and HMI target operations.
    /// </summary>
    [Trait("Category", "Integration")]
    public class HmiTargetToolsTests : TestBase
    {
        private readonly HmiTargetTools _hmiTargetTools;
        private readonly HmiTargetAdapter _hmiTargetAdapter;

        public HmiTargetToolsTests()
        {
            _hmiTargetTools = ServiceProvider.GetRequiredService<HmiTargetTools>();
            _hmiTargetAdapter = ServiceProvider.GetRequiredService<HmiTargetAdapter>();
        }

        #region hmi_targets_list Tests

        /// <summary>
        /// Test hmi_targets_list when no project is open.
        /// Should return an error response.
        /// </summary>
        [Fact]
        public void HmiTargetsList_NoProject_ReturnsError()
        {
            // Arrange - ensure no project is open
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = _hmiTargetTools.hmi_targets_list();

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }

        /// <summary>
        /// Test hmi_targets_list returns valid response structure when project is open.
        /// Response should include hmiTargetCount and hmiTargets array.
        /// </summary>
        [Fact]
        public void HmiTargetsList_WithOpenProject_ReturnsValidStructure()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            var project = sessionManager.CurrentProject;

            // Only run if project is open
            if (project == null)
            {
                // This test requires an open project - skip if none available
                return;
            }

            // Act
            var result = _hmiTargetTools.hmi_targets_list();

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);

            // Verify response contains expected properties
            dynamic data = response.Data;
            Assert.True(data["hmiTargetCount"] != null);
            Assert.True(data["hmiTargets"] != null);
        }

        #endregion

        #region hmi_targets_get Tests

        /// <summary>
        /// Test hmi_targets_get when no project is open.
        /// Should return an error response.
        /// </summary>
        [Fact]
        public void HmiTargetsGet_NoProject_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = _hmiTargetTools.hmi_targets_get("TestDevice");

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }

        /// <summary>
        /// Test hmi_targets_get with a non-existent device name.
        /// Should return an error indicating device not found.
        /// </summary>
        [Fact]
        public void HmiTargetsGet_NonExistentDevice_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            var project = sessionManager.CurrentProject;

            // Only run if project is open
            if (project == null)
            {
                return;
            }

            // Act
            var result = _hmiTargetTools.hmi_targets_get("NonExistentDevice");

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.DeviceNotFound, response.ErrorCode);
        }

        /// <summary>
        /// Test hmi_targets_get with valid device but no HMI target.
        /// Should return success with found=false.
        /// </summary>
        [Fact]
        public void HmiTargetsGet_DeviceWithoutHmiTarget_ReturnsFalse()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            var project = sessionManager.CurrentProject;

            if (project == null || project.Devices.Count == 0)
            {
                return;
            }

            // Find a device name to test with
            string testDeviceName = project.Devices[0].Name;

            // Act
            var result = _hmiTargetTools.hmi_targets_get(testDeviceName);

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);

            dynamic data = response.Data;
            // The response should indicate whether HMI target was found or not
            Assert.True(data["found"] != null);
        }

        #endregion

        #region hmi_targets_validate Tests

        /// <summary>
        /// Test hmi_targets_validate when no project is open.
        /// Should return an error response.
        /// </summary>
        [Fact]
        public void HmiTargetsValidate_NoProject_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = _hmiTargetTools.hmi_targets_validate("TestDevice");

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }

        /// <summary>
        /// Test hmi_targets_validate with a non-existent device name.
        /// Should return an error indicating device not found.
        /// </summary>
        [Fact]
        public void HmiTargetsValidate_NonExistentDevice_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            var project = sessionManager.CurrentProject;

            if (project == null)
            {
                return;
            }

            // Act
            var result = _hmiTargetTools.hmi_targets_validate("NonExistentDevice");

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.DeviceNotFound, response.ErrorCode);
        }

        /// <summary>
        /// Test hmi_targets_validate with valid device.
        /// Should return success with isValid boolean.
        /// </summary>
        [Fact]
        public void HmiTargetsValidate_ValidDevice_ReturnsSuccess()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            var project = sessionManager.CurrentProject;

            if (project == null || project.Devices.Count == 0)
            {
                return;
            }

            string testDeviceName = project.Devices[0].Name;

            // Act
            var result = _hmiTargetTools.hmi_targets_validate(testDeviceName);

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);

            dynamic data = response.Data;
            Assert.True(data["isValid"] != null);
            Assert.Equal(testDeviceName, (string)data["deviceName"]);
            Assert.True(data["message"] != null);
        }

        #endregion

        #region HmiTargetAdapter Tests

        /// <summary>
        /// Test HmiTargetAdapter.GetHmiTarget with null device.
        /// Should return null safely.
        /// </summary>
        [Fact]
        public void HmiTargetAdapter_GetHmiTarget_WithNullDevice_ReturnsNull()
        {
            // Act
            var result = _hmiTargetAdapter.GetHmiTarget(null);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Test HmiTargetAdapter.IsHmiTarget with null software.
        /// Should return false safely.
        /// </summary>
        [Fact]
        public void HmiTargetAdapter_IsHmiTarget_WithNullSoftware_ReturnsFalse()
        {
            // Act
            var result = _hmiTargetAdapter.IsHmiTarget(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Test HmiTargetAdapter.GetHmiTargetProperties with null software.
        /// Should return empty dictionary safely.
        /// </summary>
        [Fact]
        public void HmiTargetAdapter_GetHmiTargetProperties_WithNull_ReturnsEmptyDictionary()
        {
            // Act
            var result = _hmiTargetAdapter.GetHmiTargetProperties(null);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion
    }
}
