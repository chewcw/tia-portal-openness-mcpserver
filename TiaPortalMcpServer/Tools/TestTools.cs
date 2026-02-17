using System;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

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

        [McpServerTool, Description("Returns a greeting from TIA MCP Server")]
        public string hello_world()
        {
            try
            {
                _logger.LogInformation("This is a test");
                return "Hello from TIA MCP Server!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in hello_world");
                return $"Error: {ex.Message}";
            }
        }
    }
}
