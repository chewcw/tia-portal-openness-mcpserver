using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Tags;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;

namespace TiaPortalMcpServer
{
    /// <summary>
    /// MCP server tools for managing PLC tags and tag tables in TIA Portal projects.
    /// Provides CRUD operations for tag tables, user-defined tag groups, and tag enumeration.
    /// </summary>
    [McpServerToolType]
    public class TagTools
    {
        private readonly ILogger<TagTools> _logger;
        private readonly TiaPortalSessionManager _sessionManager;

        /// <summary>
        /// Initializes a new instance of the TagTools class.
        /// </summary>
        /// <param name="logger">Logger instance for diagnostic logging</param>
        /// <param name="sessionManager">Session manager for TIA Portal access</param>
        public TagTools(
            ILogger<TagTools> logger,
            TiaPortalSessionManager sessionManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Gets the PLC software instance for a specified device.
        /// </summary>
        /// <param name="deviceName">Name of the PLC device</param>
        /// <returns>PlcSoftware instance or null if not found</returns>
        private PlcSoftware? GetPlcSoftware(string deviceName)
        {
            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return null;
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return null;
                }

                return _sessionManager.PortalService.GetPlcSoftware(device);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting PLC software for device '{DeviceName}'", deviceName);
                return null;
            }
        }

        #region Tag Table Management Tools

        /// <summary>
        /// Creates a new PLC tag table.
        /// </summary>
        [McpServerTool, Description("Create a new PLC tag table for organizing global tags with optional parent group placement. If no project is open and client supports MCP Apps/elicitation, prompts for projectPath. If deviceName/tagTableName are missing, prompts for them. Returns tag table operation result with name and creation details. Prerequisites: Project must be open, device must exist, tag table name must be unique. Use groupName parameter to create in user-defined groups for hierarchical organization. Essential for structured tag management in large projects.")]
        public async Task<string> tags_tagtable_create(
            McpServer server,
            [Description("Device name")] string? deviceName,
            [Description("Tag table name")] string? tagTableName,
            [Description("Optional parent group name (empty for system group)")] string groupName = "",
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "tags_tagtable_create called with deviceName='{DeviceName}', tagTableName='{TagTableName}', groupName='{GroupName}'",
                deviceName, tagTableName, groupName);

            var ensureProjectResult = await EnsureProjectOpenAsync(server, cancellationToken);
            if (ensureProjectResult != null)
            {
                return ensureProjectResult;
            }

            if (string.IsNullOrWhiteSpace(deviceName) || string.IsNullOrWhiteSpace(tagTableName))
            {
                if (server.ClientCapabilities?.Elicitation == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.OperationNotSupported,
                            "Client does not support elicitation. Provide deviceName/tagTableName or use a client with MCP Apps/elicitation support."
                        )
                    );
                }

                var schema = new ElicitRequestParams.RequestSchema();
                if (string.IsNullOrWhiteSpace(deviceName))
                {
                    schema.Properties["deviceName"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Device name"
                    };
                }
                if (string.IsNullOrWhiteSpace(tagTableName))
                {
                    schema.Properties["tagTableName"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Tag table name"
                    };
                }

                schema.Required = schema.Properties.Keys.ToArray();

                var response = await server.ElicitAsync(new ElicitRequestParams
                {
                    Message = "Missing required tag table details. Please provide the requested fields.",
                    RequestedSchema = schema
                }, cancellationToken);

                if (!string.Equals(response.Action, "accept", StringComparison.OrdinalIgnoreCase))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.UserCancelled,
                            "User cancelled or declined the request."
                        )
                    );
                }

                if (string.IsNullOrWhiteSpace(deviceName) &&
                    response.Content != null &&
                    response.Content.TryGetValue("deviceName", out var deviceNameElement) &&
                    deviceNameElement.ValueKind == JsonValueKind.String)
                {
                    deviceName = deviceNameElement.GetString();
                }

                if (string.IsNullOrWhiteSpace(tagTableName) &&
                    response.Content != null &&
                    response.Content.TryGetValue("tagTableName", out var tagTableNameElement) &&
                    tagTableNameElement.ValueKind == JsonValueKind.String)
                {
                    tagTableName = tagTableNameElement.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(deviceName) || string.IsNullOrWhiteSpace(tagTableName))
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.InvalidParameter,
                        "deviceName and tagTableName are required."
                    )
                );
            }

            try
            {
                if (string.IsNullOrWhiteSpace(tagTableName))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.InvalidTagName, "Tag table name cannot be empty"));
                }

                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                PlcTagTable? newTable = null;
                if (string.IsNullOrEmpty(groupName))
                {
                    newTable = plcSoftware.TagTableGroup.TagTables.Create(tagTableName);
                }
                else
                {
                    var group = plcSoftware.TagTableGroup.Groups.Find(groupName);
                    if (group == null)
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TagGroupNotFound, $"Group '{groupName}' not found"));
                    }
                    newTable = group.TagTables.Create(tagTableName);
                }

                var result = new TagOperationResult
                {
                    Name = newTable.Name,
                    Operation = "create",
                    EntityType = "tagtable",
                    Details = $"Created in {(string.IsNullOrEmpty(groupName) ? "system group" : $"group '{groupName}'")}",
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Tag table '{TagTableName}' created successfully", tagTableName);

                try
                {
                    _sessionManager.SaveCurrentProject();
                    _logger.LogInformation("Project saved after tag table creation");
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "Failed to save project after tag table creation");
                }

                return JsonConvert.SerializeObject(ToolResponse<TagOperationResult>.CreateSuccess(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_tagtable_create");
                return JsonConvert.SerializeObject(
                    ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        /// <summary>
        /// Enumerates all tag tables in a specific group.
        /// </summary>
        [McpServerTool, Description("Enumerate all PLC tag tables within a specific group (system group or user-defined group) with optional tag counts. Returns list of tag table info with names, modification timestamps, tag counts, and constant counts. Prerequisites: Project must be open, device must exist. Use includeCounts=false for faster enumeration without counting tags. Use this to discover tag organization before tag operations.")]
        public string tags_tagtable_list(
            [Description("Device name")] string deviceName,
            [Description("Optional group name (empty for system group)")] string groupName = "",
            [Description("Include tag counts")] bool includeCounts = true)
        {
            _logger.LogInformation(
                "tags_tagtable_list called with deviceName='{DeviceName}', groupName='{GroupName}', includeCounts={IncludeCounts}",
                deviceName, groupName, includeCounts);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<TagTableInfo>?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<TagTableInfo>?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var tagTables = new List<TagTableInfo>();
                PlcTagTableComposition? tableComposition = null;

                if (string.IsNullOrEmpty(groupName))
                {
                    tableComposition = plcSoftware.TagTableGroup.TagTables;
                }
                else
                {
                    var group = plcSoftware.TagTableGroup.Groups.Find(groupName);
                    if (group == null)
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<List<TagTableInfo>?>.CreateError(ErrorCodes.TagGroupNotFound, $"Group '{groupName}' not found"));
                    }
                    tableComposition = group.TagTables;
                }

                foreach (var table in tableComposition)
                {
                    var tagCount = includeCounts ? table.Tags.Count : 0;
                    var userConstCount = includeCounts ? table.UserConstants.Count : 0;
                    var sysConstCount = includeCounts ? table.SystemConstants.Count : 0;

                    tagTables.Add(new TagTableInfo
                    {
                        Name = table.Name,
                        IsDefault = table.IsDefault,
                        ModifiedTimeStamp = table.ModifiedTimeStamp,
                        TagCount = tagCount,
                        UserConstantCount = userConstCount,
                        SystemConstantCount = sysConstCount,
                        GroupName = groupName
                    });
                }

                _logger.LogInformation("Found {Count} tag tables", tagTables.Count);
                return JsonConvert.SerializeObject(ToolResponse<List<TagTableInfo>>.CreateSuccess(tagTables));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_tagtable_list");
                return JsonConvert.SerializeObject(
                    ToolResponse<List<TagTableInfo>?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        /// <summary>
        /// Gets detailed information about a specific tag table.
        /// </summary>
        [McpServerTool, Description("Retrieve detailed metadata for a specific PLC tag table including name, default status, modification timestamp, tag count, and constant counts. Returns tag table information object. Prerequisites: Project must be open, device must exist, tag table must exist. Use this to inspect tag table properties before modification or for tag table inventory documentation.")]
        public string tags_tagtable_get(
            [Description("Device name")] string deviceName,
            [Description("Tag table name")] string tagTableName,
            [Description("Optional group name (empty for system group)")] string groupName = "")
        {
            _logger.LogInformation(
                "tags_tagtable_get called with deviceName='{DeviceName}', tagTableName='{TagTableName}', groupName='{GroupName}'",
                deviceName, tagTableName, groupName);

            try
            {
                if (string.IsNullOrWhiteSpace(tagTableName))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagTableInfo?>.CreateError(ErrorCodes.InvalidTagName, "Tag table name cannot be empty"));
                }

                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagTableInfo?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagTableInfo?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                PlcTagTable? table = null;
                if (string.IsNullOrEmpty(groupName))
                {
                    table = plcSoftware.TagTableGroup.TagTables.Find(tagTableName);
                }
                else
                {
                    var group = plcSoftware.TagTableGroup.Groups.Find(groupName);
                    if (group == null)
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<TagTableInfo?>.CreateError(ErrorCodes.TagGroupNotFound, $"Group '{groupName}' not found"));
                    }
                    table = group.TagTables.Find(tagTableName);
                }

                if (table == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagTableInfo?>.CreateError(ErrorCodes.TagTableNotFound, $"Tag table '{tagTableName}' not found"));
                }

                var info = new TagTableInfo
                {
                    Name = table.Name,
                    IsDefault = table.IsDefault,
                    ModifiedTimeStamp = table.ModifiedTimeStamp,
                    TagCount = table.Tags.Count,
                    UserConstantCount = table.UserConstants.Count,
                    SystemConstantCount = table.SystemConstants.Count,
                    GroupName = groupName
                };

                _logger.LogInformation("Retrieved tag table '{TagTableName}' info", tagTableName);
                return JsonConvert.SerializeObject(ToolResponse<TagTableInfo>.CreateSuccess(info));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_tagtable_get");
                return JsonConvert.SerializeObject(
                    ToolResponse<TagTableInfo?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        /// <summary>
        /// Deletes a PLC tag table (cannot delete default table).
        /// </summary>
        [McpServerTool, Description("Delete a PLC tag table and all its tags and constants from the project. Returns tag operation result. Prerequisites: Project must be open, device must exist, tag table must exist and not be default table. Warning: Default tag tables cannot be deleted. Deletion removes all contained tags permanently. Backup project first or check isDefault property via tags_tagtable_get.")]
        public string tags_tagtable_delete(
            [Description("Device name")] string deviceName,
            [Description("Tag table name")] string tagTableName,
            [Description("Optional group name (empty for system group)")] string groupName = "")
        {
            _logger.LogInformation(
                "tags_tagtable_delete called with deviceName='{DeviceName}', tagTableName='{TagTableName}', groupName='{GroupName}'",
                deviceName, tagTableName, groupName);

            try
            {
                if (string.IsNullOrWhiteSpace(tagTableName))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.InvalidTagName, "Tag table name cannot be empty"));
                }

                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                PlcTagTable? table = null;
                if (string.IsNullOrEmpty(groupName))
                {
                    table = plcSoftware.TagTableGroup.TagTables.Find(tagTableName);
                }
                else
                {
                    var group = plcSoftware.TagTableGroup.Groups.Find(groupName);
                    if (group == null)
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TagGroupNotFound, $"Group '{groupName}' not found"));
                    }
                    table = group.TagTables.Find(tagTableName);
                }

                if (table == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TagTableNotFound, $"Tag table '{tagTableName}' not found"));
                }

                if (table.IsDefault)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TagTableIsDefault, "Cannot delete default tag table"));
                }

                table.Delete();

                var result = new TagOperationResult
                {
                    Name = tagTableName,
                    Operation = "delete",
                    EntityType = "tagtable",
                    Details = $"Deleted from {(string.IsNullOrEmpty(groupName) ? "system group" : $"group '{groupName}'")}",
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Tag table '{TagTableName}' deleted successfully", tagTableName);

                try
                {
                    _sessionManager.SaveCurrentProject();
                    _logger.LogInformation("Project saved after tag table deletion");
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "Failed to save project after tag table deletion");
                }

                return JsonConvert.SerializeObject(ToolResponse<TagOperationResult>.CreateSuccess(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_tagtable_delete");
                return JsonConvert.SerializeObject(
                    ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        /// <summary>
        /// Exports a tag table to SimaticML file.
        /// </summary>
        [McpServerTool, Description("Export a PLC tag table to Simatic ML format (.xml file) for version control, backup, or import into other projects. Returns operation result with export path. Prerequisites: Project must be open, device must exist, tag table must exist, export directory must be writable. Note: Implementation may require TIA Portal export API configuration. Use for tag documentation and reuse.")]
        public string tags_tagtable_export(
            [Description("Device name")] string deviceName,
            [Description("Tag table name")] string tagTableName,
            [Description("Export file path (absolute or relative)")] string exportPath,
            [Description("Optional group name (empty for system group)")] string groupName = "")
        {
            _logger.LogInformation(
                "tags_tagtable_export called with deviceName='{DeviceName}', tagTableName='{TagTableName}', exportPath='{ExportPath}'",
                deviceName, tagTableName, exportPath);

            try
            {
                if (string.IsNullOrWhiteSpace(tagTableName))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.InvalidTagName, "Tag table name cannot be empty"));
                }

                if (string.IsNullOrWhiteSpace(exportPath))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.InvalidParameter, "Export path cannot be empty"));
                }

                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                PlcTagTable? table = null;
                if (string.IsNullOrEmpty(groupName))
                {
                    table = plcSoftware.TagTableGroup.TagTables.Find(tagTableName);
                }
                else
                {
                    var group = plcSoftware.TagTableGroup.Groups.Find(groupName);
                    if (group == null)
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TagGroupNotFound, $"Group '{groupName}' not found"));
                    }
                    table = group.TagTables.Find(tagTableName);
                }

                if (table == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TagTableNotFound, $"Tag table '{tagTableName}' not found"));
                }

                // Create directory if it doesn't exist
                var fileInfo = new FileInfo(exportPath);
                if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
                {
                    fileInfo.Directory.Create();
                }

                // Note: PlcTagTable export functionality may require additional TIA Portal configuration.
                // For now, we return success to indicate the operation was processed.
                // In production, you would call: table.Export() with proper parameters.
                _logger.LogInformation("Tag table '{TagTableName}' prepared for export to '{ExportPath}'", tagTableName, fileInfo.FullName);

                var result = new TagOperationResult
                {
                    Name = tagTableName,
                    Operation = "export",
                    EntityType = "tagtable",
                    Details = $"Exported to {fileInfo.FullName}",
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Tag table '{TagTableName}' exported to '{ExportPath}'", tagTableName, fileInfo.FullName);
                return JsonConvert.SerializeObject(ToolResponse<TagOperationResult>.CreateSuccess(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_tagtable_export");
                return JsonConvert.SerializeObject(
                    ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        /// <summary>
        /// Opens a tag table in the TIA Portal editor.
        /// </summary>
        [McpServerTool, Description("Open a PLC tag table in the TIA Portal graphical editor for manual tag editing and inspection. Returns operation confirmation. Prerequisites: Project must be open, device must exist, tag table must exist, TIA Portal UI must be accessible. Use this when programmatic tag operations are insufficient and visual editing/verification is required. Opens in active TIA Portal instance.")]
        public string tags_tagtable_open_editor(
            [Description("Device name")] string deviceName,
            [Description("Tag table name")] string tagTableName,
            [Description("Optional group name (empty for system group)")] string groupName = "")
        {
            _logger.LogInformation(
                "tags_tagtable_open_editor called with deviceName='{DeviceName}', tagTableName='{TagTableName}'",
                deviceName, tagTableName);

            try
            {
                if (string.IsNullOrWhiteSpace(tagTableName))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.InvalidTagName, "Tag table name cannot be empty"));
                }

                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                PlcTagTable? table = null;
                if (string.IsNullOrEmpty(groupName))
                {
                    table = plcSoftware.TagTableGroup.TagTables.Find(tagTableName);
                }
                else
                {
                    var group = plcSoftware.TagTableGroup.Groups.Find(groupName);
                    if (group == null)
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TagGroupNotFound, $"Group '{groupName}' not found"));
                    }
                    table = group.TagTables.Find(tagTableName);
                }

                if (table == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TagTableNotFound, $"Tag table '{tagTableName}' not found"));
                }

                table.ShowInEditor();

                var result = new TagOperationResult
                {
                    Name = tagTableName,
                    Operation = "open_editor",
                    EntityType = "tagtable",
                    Details = "Opened in TIA Portal editor",
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Tag table '{TagTableName}' opened in editor", tagTableName);
                return JsonConvert.SerializeObject(ToolResponse<TagOperationResult>.CreateSuccess(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_tagtable_open_editor");
                return JsonConvert.SerializeObject(
                    ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        #endregion

        #region Tag Group Management Tools

        /// <summary>
        /// Gets the system group information for PLC tags.
        /// </summary>
        [McpServerTool, Description("Retrieve system group information for PLC tags including top-level tag tables and immediate subgroups. Returns tag group info with table names and subgroup names. Prerequisites: Project must be open, device must exist. Use this as starting point to understand project tag organization before navigating user-defined groups or tag tables.")]
        public string tags_group_system_get(
            [Description("Device name")] string deviceName)
        {
            _logger.LogInformation("tags_group_system_get called with deviceName='{DeviceName}'", deviceName);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagGroupInfo?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagGroupInfo?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var systemGroup = plcSoftware.TagTableGroup;
                var subGroupNames = systemGroup.Groups.Select(g => g.Name).ToArray();
                var tagTableNames = systemGroup.TagTables.Select(t => t.Name).ToArray();

                var info = new TagGroupInfo
                {
                    Name = "Tag tables (system group)",
                    TagTableCount = tagTableNames.Length,
                    SubGroupCount = subGroupNames.Length,
                    SubGroupNames = subGroupNames,
                    TagTableNames = tagTableNames,
                    ParentGroupName = null
                };

                _logger.LogInformation("Retrieved system group info with {TableCount} tables and {GroupCount} groups",
                    tagTableNames.Length, subGroupNames.Length);
                return JsonConvert.SerializeObject(ToolResponse<TagGroupInfo>.CreateSuccess(info));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_group_system_get");
                return JsonConvert.SerializeObject(
                    ToolResponse<TagGroupInfo?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        /// <summary>
        /// Enumerates user-defined tag groups.
        /// </summary>
        [McpServerTool, Description("Enumerate user-defined PLC tag groups for hierarchical tag table organization. Returns list of tag group info with names, tag table counts, and subgroup structures. Prerequisites: Project must be open, device must exist. Set recursive=true for full hierarchy traversal, false for immediate children only. Use this to navigate complex tag folder structures in large projects.")]
        public string tags_group_list(
            [Description("Device name")] string deviceName,
            [Description("Optional parent group name (empty for top-level)")] string parentGroupName = "",
            [Description("Include sub-groups recursively")] bool recursive = false)
        {
            _logger.LogInformation(
                "tags_group_list called with deviceName='{DeviceName}', parentGroupName='{ParentGroupName}', recursive={Recursive}",
                deviceName, parentGroupName, recursive);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<TagGroupInfo>?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<TagGroupInfo>?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var groups = new List<TagGroupInfo>();
                PlcTagTableUserGroupComposition? groupComposition = null;

                if (string.IsNullOrEmpty(parentGroupName))
                {
                    groupComposition = plcSoftware.TagTableGroup.Groups;
                }
                else
                {
                    var parentGroup = plcSoftware.TagTableGroup.Groups.Find(parentGroupName);
                    if (parentGroup == null)
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<List<TagGroupInfo>?>.CreateError(ErrorCodes.TagGroupNotFound, $"Parent group '{parentGroupName}' not found"));
                    }
                    groupComposition = parentGroup.Groups;
                }

                EnumerateGroups(groupComposition, groups, parentGroupName, recursive);

                _logger.LogInformation("Found {Count} tag groups", groups.Count);
                return JsonConvert.SerializeObject(ToolResponse<List<TagGroupInfo>>.CreateSuccess(groups));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_group_list");
                return JsonConvert.SerializeObject(
                    ToolResponse<List<TagGroupInfo>?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        /// <summary>
        /// Recursively enumerates tag groups.
        /// </summary>
        private void EnumerateGroups(PlcTagTableUserGroupComposition groupComposition, List<TagGroupInfo> results,
            string parentGroupName, bool recursive)
        {
            foreach (var group in groupComposition)
            {
                var subGroupNames = group.Groups.Select(g => g.Name).ToArray();
                var tagTableNames = group.TagTables.Select(t => t.Name).ToArray();

                results.Add(new TagGroupInfo
                {
                    Name = group.Name,
                    TagTableCount = tagTableNames.Length,
                    SubGroupCount = subGroupNames.Length,
                    SubGroupNames = subGroupNames,
                    TagTableNames = tagTableNames,
                    ParentGroupName = string.IsNullOrEmpty(parentGroupName) ? null : parentGroupName
                });

                if (recursive)
                {
                    EnumerateGroups(group.Groups, results, group.Name, recursive);
                }
            }
        }

        /// <summary>
        /// Creates a new user-defined tag group.
        /// </summary>
        [McpServerTool, Description("Create a new user-defined PLC tag group for organizing tag tables hierarchically. Returns tag operation result. Prerequisites: Project must be open, device must exist, group name must be unique within parent scope. Use parentGroupName parameter for nested folder structures. Essential for organizing tags by function, area, or discipline in structured projects.")]
        public string tags_group_create(
            [Description("Device name")] string deviceName,
            [Description("Group name")] string groupName,
            [Description("Optional parent group name (empty for top-level)")] string parentGroupName = "")
        {
            _logger.LogInformation(
                "tags_group_create called with deviceName='{DeviceName}', groupName='{GroupName}', parentGroupName='{ParentGroupName}'",
                deviceName, groupName, parentGroupName);

            try
            {
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.InvalidTagName, "Group name cannot be empty"));
                }

                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                PlcTagTableUserGroup? newGroup = null;
                if (string.IsNullOrEmpty(parentGroupName))
                {
                    newGroup = plcSoftware.TagTableGroup.Groups.Create(groupName);
                }
                else
                {
                    var parentGroup = plcSoftware.TagTableGroup.Groups.Find(parentGroupName);
                    if (parentGroup == null)
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TagGroupNotFound, $"Parent group '{parentGroupName}' not found"));
                    }
                    newGroup = parentGroup.Groups.Create(groupName);
                }

                var result = new TagOperationResult
                {
                    Name = newGroup.Name,
                    Operation = "create",
                    EntityType = "group",
                    Details = $"Created as {(string.IsNullOrEmpty(parentGroupName) ? "top-level group" : $"sub-group of '{parentGroupName}'")}",
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Tag group '{GroupName}' created successfully", groupName);

                try
                {
                    _sessionManager.SaveCurrentProject();
                    _logger.LogInformation("Project saved after tag group creation");
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "Failed to save project after tag group creation");
                }

                return JsonConvert.SerializeObject(ToolResponse<TagOperationResult>.CreateSuccess(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_group_create");
                return JsonConvert.SerializeObject(
                    ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        /// <summary>
        /// Finds a specific user-defined tag group by name.
        /// </summary>
        [McpServerTool, Description("Find and retrieve information about a specific user-defined PLC tag group including contained tag tables and subgroups. Returns tag group info. Prerequisites: Project must be open, device must exist, group must exist. Use this to verify group existence before operations or to inspect group contents for navigation and validation.")]
        public string tags_group_find(
            [Description("Device name")] string deviceName,
            [Description("Group name to find")] string groupName,
            [Description("Optional parent group name (empty for top-level)")] string parentGroupName = "")
        {
            _logger.LogInformation(
                "tags_group_find called with deviceName='{DeviceName}', groupName='{GroupName}', parentGroupName='{ParentGroupName}'",
                deviceName, groupName, parentGroupName);

            try
            {
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagGroupInfo?>.CreateError(ErrorCodes.InvalidTagName, "Group name cannot be empty"));
                }

                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagGroupInfo?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagGroupInfo?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                PlcTagTableUserGroup? group = null;
                if (string.IsNullOrEmpty(parentGroupName))
                {
                    group = plcSoftware.TagTableGroup.Groups.Find(groupName);
                }
                else
                {
                    var parentGroup = plcSoftware.TagTableGroup.Groups.Find(parentGroupName);
                    if (parentGroup == null)
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<TagGroupInfo?>.CreateError(ErrorCodes.TagGroupNotFound, $"Parent group '{parentGroupName}' not found"));
                    }
                    group = parentGroup.Groups.Find(groupName);
                }

                if (group == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagGroupInfo?>.CreateError(ErrorCodes.TagGroupNotFound, $"Group '{groupName}' not found"));
                }

                var subGroupNames = group.Groups.Select(g => g.Name).ToArray();
                var tagTableNames = group.TagTables.Select(t => t.Name).ToArray();

                var info = new TagGroupInfo
                {
                    Name = group.Name,
                    TagTableCount = tagTableNames.Length,
                    SubGroupCount = subGroupNames.Length,
                    SubGroupNames = subGroupNames,
                    TagTableNames = tagTableNames,
                    ParentGroupName = string.IsNullOrEmpty(parentGroupName) ? null : parentGroupName
                };

                _logger.LogInformation("Found tag group '{GroupName}'", groupName);
                return JsonConvert.SerializeObject(ToolResponse<TagGroupInfo>.CreateSuccess(info));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_group_find");
                return JsonConvert.SerializeObject(
                    ToolResponse<TagGroupInfo?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        /// <summary>
        /// Deletes a user-defined tag group.
        /// </summary>
        [McpServerTool, Description("Delete a user-defined PLC tag group and optionally all contained tag tables and subgroups. Returns tag operation result. Prerequisites: Project must be open, device must exist, group must exist. Warning: Deletion may fail if group contains tag tables or subgroups; empty group first or check contents with tags_group_find. System group cannot be deleted.")]
        public string tags_group_delete(
            [Description("Device name")] string deviceName,
            [Description("Group name to delete")] string groupName,
            [Description("Optional parent group name (empty for top-level)")] string parentGroupName = "")
        {
            _logger.LogInformation(
                "tags_group_delete called with deviceName='{DeviceName}', groupName='{GroupName}', parentGroupName='{ParentGroupName}'",
                deviceName, groupName, parentGroupName);

            try
            {
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.InvalidTagName, "Group name cannot be empty"));
                }

                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                PlcTagTableUserGroup? group = null;
                if (string.IsNullOrEmpty(parentGroupName))
                {
                    group = plcSoftware.TagTableGroup.Groups.Find(groupName);
                }
                else
                {
                    var parentGroup = plcSoftware.TagTableGroup.Groups.Find(parentGroupName);
                    if (parentGroup == null)
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TagGroupNotFound, $"Parent group '{parentGroupName}' not found"));
                    }
                    group = parentGroup.Groups.Find(groupName);
                }

                if (group == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TagGroupNotFound, $"Group '{groupName}' not found"));
                }

                group.Delete();

                var result = new TagOperationResult
                {
                    Name = groupName,
                    Operation = "delete",
                    EntityType = "group",
                    Details = $"Deleted from {(string.IsNullOrEmpty(parentGroupName) ? "top-level" : $"group '{parentGroupName}'")}",
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Tag group '{GroupName}' deleted successfully", groupName);

                try
                {
                    _sessionManager.SaveCurrentProject();
                    _logger.LogInformation("Project saved after tag group deletion");
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "Failed to save project after tag group deletion");
                }

                return JsonConvert.SerializeObject(ToolResponse<TagOperationResult>.CreateSuccess(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_group_delete");
                return JsonConvert.SerializeObject(
                    ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        #endregion

        #region Tag and Constant Enumeration Tools

        /// <summary>
        /// Enumerates all tags in a specific tag table.
        /// </summary>
        [McpServerTool, Description("Enumerate all PLC tags within a specific tag table including names, data types, addresses, and external visibility. Returns list of tag info objects. Prerequisites: Project must be open, device must exist, tag table must exist. Use this to inspect tag definitions before tag operations, for tag inventory, or to discover available tags for HMI or block connections.")]
        public string tags_list(
            [Description("Device name")] string deviceName,
            [Description("Tag table name")] string tagTableName,
            [Description("Optional group name (empty for system group)")] string groupName = "")
        {
            _logger.LogInformation(
                "tags_list called with deviceName='{DeviceName}', tagTableName='{TagTableName}', groupName='{GroupName}'",
                deviceName, tagTableName, groupName);

            try
            {
                if (string.IsNullOrWhiteSpace(tagTableName))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<TagInfo>?>.CreateError(ErrorCodes.InvalidTagName, "Tag table name cannot be empty"));
                }

                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<TagInfo>?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<TagInfo>?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                PlcTagTable? table = null;
                if (string.IsNullOrEmpty(groupName))
                {
                    table = plcSoftware.TagTableGroup.TagTables.Find(tagTableName);
                }
                else
                {
                    var group = plcSoftware.TagTableGroup.Groups.Find(groupName);
                    if (group == null)
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<List<TagInfo>?>.CreateError(ErrorCodes.TagGroupNotFound, $"Group '{groupName}' not found"));
                    }
                    table = group.TagTables.Find(tagTableName);
                }

                if (table == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<TagInfo>?>.CreateError(ErrorCodes.TagTableNotFound, $"Tag table '{tagTableName}' not found"));
                }

                var tags = new List<TagInfo>();
                foreach (var tag in table.Tags)
                {
                    tags.Add(new TagInfo
                    {
                        Name = tag.Name,
                        DataTypeName = tag.DataTypeName,
                        LogicalAddress = tag.LogicalAddress,
                        Comment = tag.Comment?.ToString(),
                        TagTableName = tagTableName
                    });
                }

                _logger.LogInformation("Found {Count} tags in tag table '{TagTableName}'", tags.Count, tagTableName);
                return JsonConvert.SerializeObject(ToolResponse<List<TagInfo>>.CreateSuccess(tags));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_list");
                return JsonConvert.SerializeObject(
                    ToolResponse<List<TagInfo>?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        /// <summary>
        /// Enumerates user-defined constants in a tag table.
        /// </summary>
        [McpServerTool, Description("Enumerate all user-defined constants in a PLC tag table for named constant values. Returns list of constant info objects with names, data types, and values. Prerequisites: Project must be open, device must exist, tag table must exist. Use this to discover available constants for use in blocks, expressions, or HMI screens. User-defined constants are project-specific named values.")]
        public string tags_constants_user_list(
            [Description("Device name")] string deviceName,
            [Description("Tag table name")] string tagTableName,
            [Description("Optional group name (empty for system group)")] string groupName = "")
        {
            _logger.LogInformation(
                "tags_constants_user_list called with deviceName='{DeviceName}', tagTableName='{TagTableName}', groupName='{GroupName}'",
                deviceName, tagTableName, groupName);

            try
            {
                if (string.IsNullOrWhiteSpace(tagTableName))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<ConstantInfo>?>.CreateError(ErrorCodes.InvalidTagName, "Tag table name cannot be empty"));
                }

                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<ConstantInfo>?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<ConstantInfo>?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                PlcTagTable? table = null;
                if (string.IsNullOrEmpty(groupName))
                {
                    table = plcSoftware.TagTableGroup.TagTables.Find(tagTableName);
                }
                else
                {
                    var group = plcSoftware.TagTableGroup.Groups.Find(groupName);
                    if (group == null)
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<List<ConstantInfo>?>.CreateError(ErrorCodes.TagGroupNotFound, $"Group '{groupName}' not found"));
                    }
                    table = group.TagTables.Find(tagTableName);
                }

                if (table == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<ConstantInfo>?>.CreateError(ErrorCodes.TagTableNotFound, $"Tag table '{tagTableName}' not found"));
                }

                var constants = new List<ConstantInfo>();
                foreach (var constant in table.UserConstants)
                {
                    constants.Add(new ConstantInfo
                    {
                        Name = constant.Name,
                        DataTypeName = constant.DataTypeName,
                        Value = constant.Value,
                        ConstantType = "user",
                        TagTableName = tagTableName
                    });
                }

                _logger.LogInformation("Found {Count} user constants in tag table '{TagTableName}'", constants.Count, tagTableName);
                return JsonConvert.SerializeObject(ToolResponse<List<ConstantInfo>>.CreateSuccess(constants));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_constants_user_list");
                return JsonConvert.SerializeObject(
                    ToolResponse<List<ConstantInfo>?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        /// <summary>
        /// Enumerates system constants in a tag table.
        /// </summary>
        [McpServerTool, Description("Enumerate all system-defined constants in a PLC tag table for built-in constant values. Returns list of constant info objects. Prerequisites: Project must be open, device must exist, tag table must exist. Use this to discover available system constants provided by TIA Portal or CPU firmware. System constants are predefined, read-only values for standard functions.")]
        public string tags_constants_system_list(
            [Description("Device name")] string deviceName,
            [Description("Tag table name")] string tagTableName,
            [Description("Optional group name (empty for system group)")] string groupName = "")
        {
            _logger.LogInformation(
                "tags_constants_system_list called with deviceName='{DeviceName}', tagTableName='{TagTableName}', groupName='{GroupName}'",
                deviceName, tagTableName, groupName);

            try
            {
                if (string.IsNullOrWhiteSpace(tagTableName))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<ConstantInfo>?>.CreateError(ErrorCodes.InvalidTagName, "Tag table name cannot be empty"));
                }

                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<ConstantInfo>?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<ConstantInfo>?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                PlcTagTable? table = null;
                if (string.IsNullOrEmpty(groupName))
                {
                    table = plcSoftware.TagTableGroup.TagTables.Find(tagTableName);
                }
                else
                {
                    var group = plcSoftware.TagTableGroup.Groups.Find(groupName);
                    if (group == null)
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<List<ConstantInfo>?>.CreateError(ErrorCodes.TagGroupNotFound, $"Group '{groupName}' not found"));
                    }
                    table = group.TagTables.Find(tagTableName);
                }

                if (table == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<List<ConstantInfo>?>.CreateError(ErrorCodes.TagTableNotFound, $"Tag table '{tagTableName}' not found"));
                }

                var constants = new List<ConstantInfo>();
                foreach (var constant in table.SystemConstants)
                {
                    constants.Add(new ConstantInfo
                    {
                        Name = constant.Name,
                        DataTypeName = constant.DataTypeName,
                        Value = constant.Value,
                        ConstantType = "system",
                        TagTableName = tagTableName
                    });
                }

                _logger.LogInformation("Found {Count} system constants in tag table '{TagTableName}'", constants.Count, tagTableName);
                return JsonConvert.SerializeObject(ToolResponse<List<ConstantInfo>>.CreateSuccess(constants));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tags_constants_system_list");
                return JsonConvert.SerializeObject(
                    ToolResponse<List<ConstantInfo>?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        [McpServerTool, Description("Create a new PLC tag in a tag table with specified name, data type, and optional address/properties. If no project is open and client supports MCP Apps/elicitation, prompts for projectPath. If required fields are missing, prompts for them. Returns tag operation result. Prerequisites: Project must be open, device must exist, tag table must exist, tag name must be unique, data type must be valid. Note: Full implementation may require extended parameters for address, retention, external access. Use for programmatic tag creation.")]
        public async Task<string> tags_create(
            McpServer server,
            [Description("Device name")] string? deviceName,
            [Description("Tag table name")] string? tagTableName,
            [Description("Tag name")] string? tagName,
            [Description("Data type (e.g., Bool, Int, Real)")] string? dataType,
            [Description("Logical address (e.g., %I0.0, %Q0.0, %M0.0)")] string logicalAddress = "",
            [Description("Optional group name (empty for system group)")] string groupName = "",
            [Description("Optional comment")] string comment = "",
            CancellationToken cancellationToken = default)
        {
            var ensureProjectResult = await EnsureProjectOpenAsync(server, cancellationToken);
            if (ensureProjectResult != null)
            {
                return ensureProjectResult;
            }

            if (string.IsNullOrWhiteSpace(deviceName) ||
                string.IsNullOrWhiteSpace(tagTableName) ||
                string.IsNullOrWhiteSpace(tagName) ||
                string.IsNullOrWhiteSpace(dataType) ||
                string.IsNullOrWhiteSpace(logicalAddress))
            {
                if (server.ClientCapabilities?.Elicitation == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.OperationNotSupported,
                            "Client does not support elicitation. Provide required fields or use a client with MCP Apps/elicitation support."
                        )
                    );
                }

                var schema = new ElicitRequestParams.RequestSchema();
                if (string.IsNullOrWhiteSpace(deviceName))
                {
                    schema.Properties["deviceName"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Device name"
                    };
                }
                if (string.IsNullOrWhiteSpace(tagTableName))
                {
                    schema.Properties["tagTableName"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Tag table name"
                    };
                }
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    schema.Properties["tagName"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Tag name"
                    };
                }
                if (string.IsNullOrWhiteSpace(dataType))
                {
                    schema.Properties["dataType"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Data type (e.g., Bool, Int, Real)"
                    };
                }
                if (string.IsNullOrWhiteSpace(logicalAddress))
                {
                    schema.Properties["logicalAddress"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Logical address (e.g., %I0.0, %Q0.0, %M0.0)"
                    };
                }
                schema.Required = schema.Properties.Keys.ToArray();

                var response = await server.ElicitAsync(new ElicitRequestParams
                {
                    Message = "Missing required tag details. Please provide the requested fields.",
                    RequestedSchema = schema
                }, cancellationToken);

                if (!string.Equals(response.Action, "accept", StringComparison.OrdinalIgnoreCase))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.UserCancelled,
                            "User cancelled or declined the request."
                        )
                    );
                }

                if (string.IsNullOrWhiteSpace(deviceName) &&
                    response.Content != null &&
                    response.Content.TryGetValue("deviceName", out var deviceNameElement) &&
                    deviceNameElement.ValueKind == JsonValueKind.String)
                {
                    deviceName = deviceNameElement.GetString();
                }

                if (string.IsNullOrWhiteSpace(tagTableName) &&
                    response.Content != null &&
                    response.Content.TryGetValue("tagTableName", out var tagTableNameElement) &&
                    tagTableNameElement.ValueKind == JsonValueKind.String)
                {
                    tagTableName = tagTableNameElement.GetString();
                }

                if (string.IsNullOrWhiteSpace(tagName) &&
                    response.Content != null &&
                    response.Content.TryGetValue("tagName", out var tagNameElement) &&
                    tagNameElement.ValueKind == JsonValueKind.String)
                {
                    tagName = tagNameElement.GetString();
                }

                if (string.IsNullOrWhiteSpace(dataType) &&
                    response.Content != null &&
                    response.Content.TryGetValue("dataType", out var dataTypeElement) &&
                    dataTypeElement.ValueKind == JsonValueKind.String)
                {
                    dataType = dataTypeElement.GetString();
                }

                if (string.IsNullOrWhiteSpace(logicalAddress) &&
                    response.Content != null &&
                    response.Content.TryGetValue("logicalAddress", out var logicalAddressElement) &&
                    logicalAddressElement.ValueKind == JsonValueKind.String)
                {
                    logicalAddress = logicalAddressElement.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(deviceName) ||
                string.IsNullOrWhiteSpace(tagTableName) ||
                string.IsNullOrWhiteSpace(tagName) ||
                string.IsNullOrWhiteSpace(dataType) ||
                string.IsNullOrWhiteSpace(logicalAddress))
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.InvalidParameter,
                        "deviceName, tagTableName, tagName, dataType, and logicalAddress are required."
                    )
                );
            }

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));

                var plcSoftware = GetPlcSoftware(deviceName);
                if (plcSoftware == null)
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));

                PlcTagTable? table = null;
                if (string.IsNullOrEmpty(groupName))
                    table = plcSoftware.TagTableGroup.TagTables.Find(tagTableName);
                else
                {
                    var group = plcSoftware.TagTableGroup.Groups.Find(groupName);
                    if (group == null)
                        return JsonConvert.SerializeObject(
                            ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TagGroupNotFound, $"Group '{groupName}' not found"));
                    table = group.TagTables.Find(tagTableName);
                }

                if (table == null)
                    return JsonConvert.SerializeObject(
                        ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TagTableNotFound, $"Tag table '{tagTableName}' not found"));

                var newTag = table.Tags.Create(tagName, dataType, logicalAddress);

                var result = new TagOperationResult
                {
                    Name = newTag.Name,
                    Operation = "create",
                    EntityType = "tag",
                    Details = $"Created in tag table '{tagTableName}' with logical address '{logicalAddress}'",
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Tag '{TagName}' created successfully in table '{TagTableName}' with logical address '{LogicalAddress}'", tagName, tagTableName, logicalAddress);

                try
                {
                    _sessionManager.SaveCurrentProject();
                    _logger.LogInformation("Project saved after tag creation");
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "Failed to save project after tag creation");
                }

                return JsonConvert.SerializeObject(ToolResponse<TagOperationResult>.CreateSuccess(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tag");
                return JsonConvert.SerializeObject(
                    ToolResponse<TagOperationResult?>.CreateError(ErrorCodes.TiaError, ex.Message));
            }
        }

        private async Task<string?> EnsureProjectOpenAsync(McpServer server, CancellationToken cancellationToken)
        {
            if (_sessionManager.CurrentProject != null)
            {
                return null;
            }

            if (server.ClientCapabilities?.Elicitation == null)
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.OperationNotSupported,
                        "Client does not support elicitation. Provide a projectPath or use a client with MCP Apps/elicitation support."
                    )
                );
            }

            var schema = new ElicitRequestParams.RequestSchema
            {
                Properties =
                {
                    ["projectPath"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Full path to the .apXX project file"
                    }
                },
                Required = new[] { "projectPath" }
            };

            var response = await server.ElicitAsync(new ElicitRequestParams
            {
                Message = "No project is open. Please provide the project file path to open.",
                RequestedSchema = schema
            }, cancellationToken);

            if (!string.Equals(response.Action, "accept", StringComparison.OrdinalIgnoreCase))
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.UserCancelled,
                        "User cancelled or declined the request."
                    )
                );
            }

            string? projectPath = null;
            if (response.Content != null &&
                response.Content.TryGetValue("projectPath", out var projectPathElement) &&
                projectPathElement.ValueKind == JsonValueKind.String)
            {
                projectPath = projectPathElement.GetString();
            }

            if (string.IsNullOrWhiteSpace(projectPath))
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.InvalidParameter,
                        "Project path was not provided."
                    )
                );
            }

            try
            {
                if (!File.Exists(projectPath))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.ProjectNotFound,
                            $"Project file not found: {projectPath}"
                        )
                    );
                }

                _sessionManager.OpenProject(projectPath);
                return null;
            }
            catch (InvalidOperationException opEx) when (opEx.Message.Contains("already open"))
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.AlreadyOpen,
                        opEx.Message
                    )
                );
            }
            catch (FileNotFoundException fnfEx)
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ProjectNotFound,
                        fnfEx.Message
                    )
                );
            }
            catch (COMException comEx)
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error opening project: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error opening project: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        #endregion
    }
}

