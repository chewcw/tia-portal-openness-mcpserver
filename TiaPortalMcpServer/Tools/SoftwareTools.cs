using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public class SoftwareTools
    {
        private readonly ILogger<SoftwareTools> _logger;
        private readonly TiaPortalSessionManager _sessionManager;

        public SoftwareTools(
            ILogger<SoftwareTools> logger,
            TiaPortalSessionManager sessionManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
        }

        [McpServerTool, Description("Add a PLC block to a device")]
        public string add_block(
            [Description("Device name")] string deviceName,
            [Description("Block type (FB, FC, DB, OB)")] string blockType,
            [Description("Block name or number")] string blockName)
        {
            _logger.LogInformation("add_block called with deviceName='{DeviceName}', blockType='{BlockType}', blockName='{BlockName}'", deviceName, blockType, blockName);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.NoProject,
                            "No project is currently open. Use open_project first."
                        )
                    );
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.DeviceNotFound,
                            $"Device '{deviceName}' not found in project"
                        )
                    );
                }

                var software = TiaPortalSoftwareHelper.TryGetPlcSoftware(device);
                if (software == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.TiaError,
                            $"Device '{deviceName}' does not have PLC software"
                        )
                    );
                }

                var blockGroup = software.BlockGroup;
                if (blockGroup == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.TiaError,
                            "Block group not accessible"
                        )
                    );
                }

                // Check if block already exists
                var existingBlock = blockGroup.Blocks.FirstOrDefault(b => b.Name == blockName);
                if (existingBlock != null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.BlockExists,
                            $"Block '{blockName}' already exists"
                        )
                    );
                }

                // Create the block based on type
                PlcBlock? newBlock = CreatePlcBlock(blockGroup.Blocks, blockType, blockName);
                if (newBlock == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.InvalidParameter,
                            $"Unable to create block type '{blockType}'. Check TIA Portal API support for this block type."
                        )
                    );
                }

                _logger.LogInformation("Block '{BlockName}' of type '{BlockType}' created successfully", blockName, blockType);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        blockName = newBlock?.Name,
                        blockType = blockType,
                        deviceName = deviceName,
                        message = $"Block '{blockName}' created successfully"
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error adding block '{BlockName}'", blockName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error adding block: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding block '{BlockName}'", blockName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error adding block: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("List all blocks in a device")]
        public string list_blocks([Description("Device name")] string deviceName)
        {
            _logger.LogInformation("list_blocks called with deviceName='{DeviceName}'", deviceName);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.NoProject,
                            "No project is currently open. Use open_project first."
                        )
                    );
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.DeviceNotFound,
                            $"Device '{deviceName}' not found in project"
                        )
                    );
                }

                var software = TiaPortalSoftwareHelper.TryGetPlcSoftware(device);
                if (software == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.TiaError,
                            $"Device '{deviceName}' does not have PLC software"
                        )
                    );
                }

                var blocks = software.BlockGroup?.Blocks
                    .Select(block => new
                    {
                        name = block.Name,
                        type = block.GetType().Name,
                        programmingLanguage = (block as IEngineeringObject)?.GetAttribute("ProgrammingLanguage")?.ToString()
                    })
                    .Cast<object>()
                    .ToList();

                _logger.LogInformation("Found {Count} blocks in device '{DeviceName}'", blocks?.Count ?? 0, deviceName);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        deviceName = deviceName,
                        blockCount = blocks?.Count ?? 0,
                        blocks = blocks ?? new System.Collections.Generic.List<object>()
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error listing blocks for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error listing blocks: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing blocks for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error listing blocks: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Get hierarchical view of block groups and blocks in a device")]
        public string get_block_hierarchy(
            [Description("Device name")] string deviceName,
            [Description("Include blocks in response (default: true)")] bool includeBlocks = true)
        {
            _logger.LogInformation("get_block_hierarchy called with deviceName='{DeviceName}', includeBlocks={IncludeBlocks}", deviceName, includeBlocks);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.NoProject,
                            "No project is currently open. Use open_project first."
                        )
                    );
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.DeviceNotFound,
                            $"Device '{deviceName}' not found in project"
                        )
                    );
                }

                var software = TiaPortalSoftwareHelper.TryGetPlcSoftware(device);
                if (software == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.TiaError,
                            $"Device '{deviceName}' does not have PLC software"
                        )
                    );
                }

                var blockGroup = software.BlockGroup;
                if (blockGroup == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.TiaError,
                            "Block group not accessible"
                        )
                    );
                }

                // Get all blocks at root level
                var rootBlocks = includeBlocks
                    ? EnumerateBlocks(blockGroup.Blocks)
                    : new System.Collections.Generic.List<object>();

                // Get all user-defined groups recursively
                var userGroups = new System.Collections.Generic.List<object>();
                foreach (PlcBlockUserGroup blockUserGroup in blockGroup.Groups)
                {
                    userGroups.Add(EnumerateBlockUserGroup(blockUserGroup, includeBlocks));
                }

                var totalBlockCount = CountAllBlocks(blockGroup);

                _logger.LogInformation("Found {GroupCount} user groups and {BlockCount} total blocks in device '{DeviceName}'",
                    userGroups.Count, totalBlockCount, deviceName);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        deviceName = deviceName,
                        totalBlockCount = totalBlockCount,
                        rootBlocks = rootBlocks,
                        userGroups = userGroups,
                        message = $"Retrieved block hierarchy with {userGroups.Count} user groups and {totalBlockCount} blocks"
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error getting block hierarchy for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error getting block hierarchy: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting block hierarchy for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error getting block hierarchy: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Add a tag/variable to PLC tag table")]
        public string add_tag(
            [Description("Device name")] string deviceName,
            [Description("Tag name")] string tagName,
            [Description("Data type (e.g., Bool, Int, Real)")] string dataType)
        {
            _logger.LogInformation("add_tag called with deviceName='{DeviceName}', tagName='{TagName}', dataType='{DataType}'", deviceName, tagName, dataType);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.NoProject,
                            "No project is currently open. Use open_project first."
                        )
                    );
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.DeviceNotFound,
                            $"Device '{deviceName}' not found in project"
                        )
                    );
                }

                var software = TiaPortalSoftwareHelper.TryGetPlcSoftware(device);
                if (software == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.TiaError,
                            $"Device '{deviceName}' does not have PLC software"
                        )
                    );
                }

                var tagTableGroup = software.TagTableGroup;
                if (tagTableGroup == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.TiaError,
                            "Tag table group not accessible"
                        )
                    );
                }

                // Get or create default tag table
                var tagTable = tagTableGroup.TagTables.FirstOrDefault();
                if (tagTable == null)
                {
                    tagTable = tagTableGroup.TagTables.Create("DefaultTagTable");
                }

                // Create the tag via reflection (some API versions require logical address)
                var newTag = CreateTag(tagTable.Tags, tagName, dataType);
                if (newTag == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.TiaError,
                            "Unable to create tag with provided parameters"
                        )
                    );
                }

                _logger.LogInformation("Tag '{TagName}' of type '{DataType}' created successfully", tagName, dataType);

                var createdTagName = GetObjectName(newTag) ?? tagName;

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        tagName = createdTagName,
                        dataType = dataType,
                        deviceName = deviceName,
                        tagTable = tagTable.Name,
                        message = $"Tag '{createdTagName}' created successfully"
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error adding tag '{TagName}'", tagName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error adding tag: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding tag '{TagName}'", tagName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error adding tag: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        private static PlcBlock? CreatePlcBlock(PlcBlockComposition blockComposition, string blockType, string blockName)
        {
            if (blockComposition == null)
            {
                return null;
            }

            var methodName = blockType.ToUpper() switch
            {
                "FB" => "CreateFB",
                "FC" => "CreateFC",
                "DB" => "CreateDB",
                "OB" => "CreateOB",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            var methods = blockComposition.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == methodName)
                .ToList();

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();

                // Try common overloads by parameter count
                if (parameters.Length == 1)
                {
                    return method.Invoke(blockComposition, new object[] { blockName }) as PlcBlock;
                }

                if (parameters.Length == 2 && int.TryParse(blockName, out var number))
                {
                    return method.Invoke(blockComposition, new object[] { blockName, number }) as PlcBlock;
                }

                if (parameters.Length >= 3)
                {
                    // Try to provide minimal defaults for required parameters
                    var args = new object?[parameters.Length];
                    args[0] = blockName;
                    for (var i = 1; i < parameters.Length; i++)
                    {
                        args[i] = parameters[i].ParameterType.IsValueType
                            ? Activator.CreateInstance(parameters[i].ParameterType)!
                            : null;
                    }

                    return method.Invoke(blockComposition, args) as PlcBlock;
                }
            }

            return null;
        }

        private static object? CreateTag(object tagComposition, string tagName, string dataType)
        {
            if (tagComposition == null)
            {
                return null;
            }

            var methods = tagComposition.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "Create")
                .ToList();

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 2)
                {
                    return method.Invoke(tagComposition, new object[] { tagName, dataType });
                }

                if (parameters.Length == 3)
                {
                    return method.Invoke(tagComposition, new object[] { tagName, dataType, string.Empty });
                }
            }

            return null;
        }

        private static string? GetObjectName(object instance)
        {
            var nameProperty = instance.GetType().GetProperty("Name");
            return nameProperty?.GetValue(instance)?.ToString();
        }

        /// <summary>
        /// Recursively enumerates a block user group and its subgroups
        /// </summary>
        private static object EnumerateBlockUserGroup(PlcBlockUserGroup blockUserGroup, bool includeBlocks)
        {
            var subGroups = new System.Collections.Generic.List<object>();
            foreach (PlcBlockUserGroup subBlockUserGroup in blockUserGroup.Groups)
            {
                subGroups.Add(EnumerateBlockUserGroup(subBlockUserGroup, includeBlocks));
            }

            var blocks = includeBlocks
                ? EnumerateBlocks(blockUserGroup.Blocks)
                : new System.Collections.Generic.List<object>();

            return new
            {
                name = blockUserGroup.Name,
                blockCount = blockUserGroup.Blocks.Count,
                subGroupCount = blockUserGroup.Groups.Count,
                blocks = blocks,
                subGroups = subGroups
            };
        }

        /// <summary>
        /// Enumerates all blocks in a block composition
        /// </summary>
        private static System.Collections.Generic.List<object> EnumerateBlocks(PlcBlockComposition blockComposition)
        {
            var blocks = new System.Collections.Generic.List<object>();
            foreach (PlcBlock block in blockComposition)
            {
                blocks.Add(new
                {
                    name = block.Name,
                    type = block.GetType().Name,
                    programmingLanguage = (block as IEngineeringObject)?.GetAttribute("ProgrammingLanguage")?.ToString()
                });
            }
            return blocks;
        }

        /// <summary>
        /// Counts all blocks in a block group including subgroups
        /// </summary>
        private static int CountAllBlocks(PlcBlockGroup blockGroup)
        {
            var count = blockGroup.Blocks.Count;
            foreach (PlcBlockUserGroup userGroup in blockGroup.Groups)
            {
                count += CountBlocksInUserGroup(userGroup);
            }
            return count;
        }

        /// <summary>
        /// Recursively counts blocks in a user group and its subgroups
        /// </summary>
        private static int CountBlocksInUserGroup(PlcBlockUserGroup userGroup)
        {
            var count = userGroup.Blocks.Count;
            foreach (PlcBlockUserGroup subGroup in userGroup.Groups)
            {
                count += CountBlocksInUserGroup(subGroup);
            }
            return count;
        }
    }
}
