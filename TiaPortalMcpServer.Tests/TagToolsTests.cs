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
    /// <summary>
    /// Unit tests for TagTools MCP server tools.
    /// Tests tag table management, tag group operations, and tag enumeration.
    /// </summary>
    [Trait("Category", "Integration")]
    public class TagToolsTests : TestBase
    {
        private readonly TagTools _tagTools;

        public TagToolsTests()
        {
            _tagTools = ServiceProvider.GetRequiredService<TagTools>();
        }

        #region Tag Table Tests - Phase 4

        // Test tags_tagtable_create
        // Test tags_tagtable_list
        // Test tags_tagtable_get
        // Test tags_tagtable_delete
        // Test tags_tagtable_export
        // Test tags_tagtable_open_editor

        #endregion

        #region Tag Group Tests - Phase 4

        // Test tags_group_system_get
        // Test tags_group_list
        // Test tags_group_create
        // Test tags_group_find
        // Test tags_group_delete

        #endregion

        #region Tag & Constant Tests - Phase 4

        // Test tags_list
        // Test tags_constants_user_list
        // Test tags_constants_system_list

        #endregion

        #region Error Handling Tests - Phase 4

        /// <summary>
        /// Test that tags_tagtable_create returns error when no project is open.
        /// </summary>
        [Fact]
        public async Task TagsTableCreate_NoProject_ReturnsError()
        {
            // Arrange
            var sessionManager = ServiceProvider.GetRequiredService<TiaPortalSessionManager>();
            sessionManager.CloseCurrentProject();

            // Act
            var result = await _tagTools.tags_tagtable_create(null, "TestDevice", "TestTable", cancellationToken: CancellationToken.None);

            // Assert
            var response = JsonConvert.DeserializeObject<ToolResponse<object>>(result);
            Assert.False(response.Success);
            Assert.Equal(ErrorCodes.NoProject, response.ErrorCode);
        }

        #endregion
    }
}
