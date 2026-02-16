using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public static class ProjectManagementTools
    {
        [McpServerTool, Description("Create a new TIA project")]
        public static string create_project([Description("Name of the project")] string name, [Description("Path to save the project")] string path)
        {
            // TODO: Implement using TIA Openness API
            // var tiaPortal = new TiaPortal();
            // var project = tiaPortal.Projects.Create(path, name);
            // return project.Id.ToString();
            return $"Project '{name}' would be created at '{path}'";
        }

        [McpServerTool, Description("Open an existing TIA project")]
        public static string open_project([Description("Path to the project file")] string path)
        {
            // TODO: Implement using TIA Openness API
            // var tiaPortal = new TiaPortal();
            // var project = tiaPortal.Projects.Open(path);
            // return project.Name;
            return $"Project at '{path}' would be opened";
        }

        [McpServerTool, Description("Save the current project")]
        public static string save_project([Description("Project ID")] string projectId)
        {
            // TODO: Implement
            return $"Project '{projectId}' would be saved";
        }

        [McpServerTool, Description("Close a TIA project")]
        public static string close_project([Description("Project ID")] string projectId)
        {
            // TODO: Implement
            return $"Project '{projectId}' would be closed";
        }
    }
}
