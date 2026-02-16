using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public static class TestTools
    {
        [McpServerTool, Description("Returns a greeting from TIA MCP Server")]
        public static string hello_world() => "Hello from TIA MCP Server!";
    }
}
