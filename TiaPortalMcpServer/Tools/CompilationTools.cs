using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public static class CompilationTools
    {
        [McpServerTool, Description("Compile the entire project")]
        public static string compile_project([Description("Project ID")] string projectId)
        {
            // TODO: Implement
            return $"Project '{projectId}' would be compiled";
        }

        [McpServerTool, Description("Compile PLC software")]
        public static string compile_software([Description("Device ID")] string deviceId)
        {
            // TODO: Implement
            return $"Software in device '{deviceId}' would be compiled";
        }
    }
}
