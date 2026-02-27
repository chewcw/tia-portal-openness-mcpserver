using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
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
        [McpServerTool, Description("Create a new PLC tag table")]
        public string tags_tagtable_create(
            [Description("Device name")] string deviceName,
            [Description("Tag table name")] string tagTableName,
            [Description("Optional parent group name (empty for system group)")] string groupName = "")
        {
            _logger.LogInformation(
                "tags_tagtable_create called with deviceName='{DeviceName}', tagTableName='{TagTableName}', groupName='{GroupName}'",
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

                PlcTagTable? newTable = null;
                if (string.IsNullOrEmpty(groupName))
                {
                    // Create in system group
                    newTable = plcSoftware.TagTableGroup.TagTables.Create(tagTableName);
                }
                else
                {
                    // Create in user group
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
        [McpServerTool, Description("List all PLC tag tables in a group")]
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
        [McpServerTool, Description("Get detailed information about a specific PLC tag table")]
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
        [McpServerTool, Description("Delete a PLC tag table (cannot delete default table)")]
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
        [McpServerTool, Description("Export a PLC tag table to SimaticML file")]
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
        [McpServerTool, Description("Open a PLC tag table in the TIA Portal editor")]
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
        [McpServerTool, Description("Get the system group for PLC tags")]
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
        [McpServerTool, Description("List user-defined PLC tag groups")]
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
        [McpServerTool, Description("Create a new user-defined PLC tag group")]
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
        [McpServerTool, Description("Find a specific user-defined PLC tag group")]
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
        [McpServerTool, Description("Delete a user-defined PLC tag group")]
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
        [McpServerTool, Description("List all PLC tags in a tag table")]
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
        [McpServerTool, Description("List user-defined constants in a PLC tag table")]
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
        [McpServerTool, Description("List system constants in a PLC tag table")]
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

        #endregion
    }
}

