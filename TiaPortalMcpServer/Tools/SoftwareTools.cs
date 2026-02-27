using System;
using System.Collections.Generic;
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
        public string software_add_block(
            [Description("Device name")] string deviceName,
            [Description("Block type (FB, FC, DB, OB)")] string blockType,
            [Description("Block name or number")] string blockName)
        {
            _logger.LogInformation("software_add_block called with deviceName='{DeviceName}', blockType='{BlockType}', blockName='{BlockName}'", deviceName, blockType, blockName);

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

                var software = _sessionManager.PortalService.GetPlcSoftware(device);
                if (software == null)
                {
                    _logger.LogWarning("Failed to get PLC software for device '{DeviceName}'", deviceName);
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
        public string blocks_list([Description("Device name")] string deviceName)
        {
            _logger.LogInformation("blocks_list called with deviceName='{DeviceName}'", deviceName);

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

                var blocks = _sessionManager.PortalService.GetBlocks(project, deviceName)
                    .Select(block => new
                    {
                        name = block.Name,
                        type = block.GetType().Name,
                        programmingLanguage = (block as IEngineeringObject)?.GetAttribute("ProgrammingLanguage")?.ToString(),
                        autoNumber = block.AutoNumber,
                        creationDate = block.CreationDate,
                        modifiedDate = block.ModifiedDate,
                        codeModifiedDate = block.CodeModifiedDate,
                        compileDate = block.CompileDate,
                    })
                    .Cast<object>()
                    .ToList();

                _logger.LogInformation("Found {Count} blocks in device '{DeviceName}'", blocks?.Count ?? 0, deviceName);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        deviceName = deviceName,
                        blockCount = blocks?.Count ?? 0,
                        blocks = blocks ?? new List<object>(),
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
        public string software_get_block_hierarchy(
            [Description("Device name")] string deviceName,
            [Description("Include blocks in response (default: true)")] bool includeBlocks = true)
        {
            _logger.LogInformation("software_get_block_hierarchy called with deviceName='{DeviceName}', includeBlocks={IncludeBlocks}", deviceName, includeBlocks);

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

                var software = _sessionManager.PortalService.GetPlcSoftware(device);
                if (software == null)
                {
                    _logger.LogWarning("Failed to get PLC software for device '{DeviceName}'", deviceName);
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

        /// <summary>
        /// Creates a PLC block with the specified type and name using reflection.
        /// This approach is necessary due to varying TIA Portal API signatures across versions.
        /// </summary>
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
                try
                {
                    // Try different parameter combinations that were working before
                    if (parameters.Length == 1)
                    {
                        // Simple case: CreateX(name)
                        return method.Invoke(blockComposition, new object[] { blockName }) as PlcBlock;
                    }

                    if (parameters.Length == 2 && int.TryParse(blockName, out var number))
                    {
                        // CreateX(name, number)
                        return method.Invoke(blockComposition, new object[] { blockName, number }) as PlcBlock;
                    }

                    if (parameters.Length >= 3)
                    {
                        // Try to provide minimal defaults for required parameters
                        var args = new object?[parameters.Length];
                        args[0] = blockName;

                        // Fill in default values for other parameters
                        for (var i = 1; i < parameters.Length; i++)
                        {
                            var paramType = parameters[i].ParameterType;
                            if (paramType == typeof(bool))
                            {
                                args[i] = false; // isAutoNumbered = false
                            }
                            else if (paramType == typeof(int))
                            {
                                args[i] = 0; // number = 0 or default
                            }
                            else if (paramType.IsEnum)
                            {
                                // For enums like ProgrammingLanguage, use first/default value
                                args[i] = Enum.GetValues(paramType).GetValue(0);
                            }
                            else if (paramType.IsValueType)
                            {
                                args[i] = Activator.CreateInstance(paramType);
                            }
                            else
                            {
                                args[i] = null;
                            }
                        }

                        return method.Invoke(blockComposition, args) as PlcBlock;
                    }
                }
                catch (Exception)
                {
                    // If this method signature fails, try the next one
                    continue;
                }
            }

            return null;
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
