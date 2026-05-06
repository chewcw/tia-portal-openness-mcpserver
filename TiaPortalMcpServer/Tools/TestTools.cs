using System;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using TiaPortalMcpServer.Models;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public class TestTools
    {
        private readonly ILogger<TestTools> _logger;

        public TestTools(ILogger<TestTools> logger)
        {
            _logger = logger;
        }

        [McpServerTool(ReadOnly = true, Idempotent = true), Description("Returns a greeting from TIA MCP Server")]
        public CallToolResult hello_world()
        {
            try
            {
                _logger.LogInformation("This is a test");
                return McpToolResults.Success(new { message = "Hello from TIA MCP Server!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in hello_world");
                return McpToolResults.Error(ErrorCodes.InternalError, $"Error: {ex.Message}", ex.ToString());
            }
        }
    }
}
