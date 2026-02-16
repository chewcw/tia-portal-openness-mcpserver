using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TiaPortalMcpServer
{
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
}
