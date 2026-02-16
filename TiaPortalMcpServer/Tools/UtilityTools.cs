using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public static class UtilityTools
    {
        [McpServerTool, Description("Get project metadata")]
        public static string get_project_info([Description("Project ID")] string projectId)
        {
            // TODO: Implement
            return $"Info for project '{projectId}': [placeholder info]";
        }

        [McpServerTool, Description("List available TIA libraries")]
        public static string list_available_libraries()
        {
            // TODO: Implement
            return "Available libraries: [placeholder list]";
        }
    }
}
