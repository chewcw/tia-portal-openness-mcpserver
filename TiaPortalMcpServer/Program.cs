// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();

[McpServerToolType]
public static class TestTools
{
    [McpServerTool, Description("Returns a greeting from TIA MCP Server")]
    public static string hello_world() => "Hello from TIA MCP Server!";
}

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

[McpServerToolType]
public static class HardwareTools
{
    [McpServerTool, Description("Add a hardware device to the project")]
    public static string add_device(
        [Description("Project ID")] string projectId,
        [Description("Device type (e.g., S7-1200)")] string deviceType,
        [Description("Device name")] string name,
        [Description("Order number")] string orderNumber)
    {
        // TODO: Implement using TIA Openness API
        return $"Device '{name}' of type '{deviceType}' would be added to project '{projectId}'";
    }

    [McpServerTool, Description("List devices in the project")]
    public static string list_devices([Description("Project ID")] string projectId)
    {
        // TODO: Implement
        return $"Devices in project '{projectId}': [placeholder list]";
    }

    [McpServerTool, Description("Configure device parameters")]
    public static string configure_device(
        [Description("Device ID")] string deviceId,
        [Description("Parameters as JSON string")] string parameters)
    {
        // TODO: Implement
        return $"Device '{deviceId}' would be configured with parameters: {parameters}";
    }
}

[McpServerToolType]
public static class SoftwareTools
{
    [McpServerTool, Description("Add a PLC block")]
    public static string add_block(
        [Description("Device ID")] string deviceId,
        [Description("Block type (e.g., FB, FC)")] string blockType,
        [Description("Block name")] string name)
    {
        // TODO: Implement
        return $"Block '{name}' of type '{blockType}' would be added to device '{deviceId}'";
    }

    [McpServerTool, Description("List blocks in the device")]
    public static string list_blocks([Description("Device ID")] string deviceId)
    {
        // TODO: Implement
        return $"Blocks in device '{deviceId}': [placeholder list]";
    }

    [McpServerTool, Description("Add a variable/tag")]
    public static string add_tag(
        [Description("Block ID or Device ID")] string parentId,
        [Description("Tag name")] string name,
        [Description("Data type")] string dataType)
    {
        // TODO: Implement
        return $"Tag '{name}' of type '{dataType}' would be added to '{parentId}'";
    }
}

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
