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
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Blocks.Interface;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Types;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public class BlocksTools
    {
        private readonly ILogger<BlocksTools> _logger;
        private readonly TiaPortalSessionManager _sessionManager;
        private readonly BlocksAdapter _blocksAdapter;

        public BlocksTools(
            ILogger<BlocksTools> logger,
            TiaPortalSessionManager sessionManager,
            BlocksAdapter blocksAdapter)
        {
            _logger = logger;
            _sessionManager = sessionManager;
            _blocksAdapter = blocksAdapter;
        }

        #region ProDiag Tools

        [McpServerTool, Description("Get assigned ProDiag FB for a DB member or tag member")]
        public string blocks_prodiag_assigned_get(
            [Description("Device name")] string deviceName,
            [Description("Path to member (e.g., 'DB_Name.SimpleUdtInstance' or 'ArrayUdtMember.ArrayUdtMember[0]')")] string memberPath,
            [Description("Member type: 'db' for DB member, 'tag' for tag table member")] string memberType)
        {
            _logger.LogInformation("blocks_prodiag_assigned_get called with deviceName='{DeviceName}', memberPath='{MemberPath}', memberType='{MemberType}'", deviceName, memberPath, memberType);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<string?>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<string?>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<string?>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                IEngineeringObject? member = null;
                if (memberType.ToLowerInvariant() == "db")
                {
                    var dataBlock = plcSoftware.BlockGroup.Blocks.Find(memberPath.Split('.')[0]);
                    if (dataBlock == null)
                    {
                        return JsonConvert.SerializeObject(ToolResponse<string?>.CreateError(ErrorCodes.BlockNotFound, $"DB '{memberPath.Split('.')[0]}' not found"));
                    }

                    // Try to get interface from the block
                    var blockInterface = dataBlock.GetType().GetProperty("Interface")?.GetValue(dataBlock);
                    if (blockInterface == null)
                    {
                        return JsonConvert.SerializeObject(ToolResponse<string?>.CreateError(ErrorCodes.TiaError, "Block does not have an accessible interface"));
                    }

                    var members = blockInterface.GetType().GetProperty("Members")?.GetValue(blockInterface) as MemberComposition;
                    if (members == null)
                    {
                        return JsonConvert.SerializeObject(ToolResponse<string?>.CreateError(ErrorCodes.TiaError, "Block interface does not have members"));
                    }

                    member = FindMemberByPath(members, memberPath.Substring(memberPath.IndexOf('.') + 1));
                }
                else if (memberType.ToLowerInvariant() == "tag")
                {
                    var tagTable = plcSoftware.TagTableGroup.TagTables.Find(memberPath.Split('.')[0]);
                    if (tagTable == null)
                    {
                        return JsonConvert.SerializeObject(ToolResponse<string?>.CreateError(ErrorCodes.InvalidParameter, $"Tag table '{memberPath.Split('.')[0]}' not found"));
                    }

                    var tag = tagTable.Tags.Find(memberPath.Substring(memberPath.IndexOf('.') + 1));
                    member = tag;
                }
                else
                {
                    return JsonConvert.SerializeObject(ToolResponse<string?>.CreateError(ErrorCodes.InvalidParameter, "memberType must be 'db' or 'tag'"));
                }

                if (member == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<string?>.CreateError(ErrorCodes.InvalidParameter, $"Member '{memberPath}' not found"));
                }

                var result = _blocksAdapter.GetAssignedProDiagFb(member);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_prodiag_assigned_get");
                return JsonConvert.SerializeObject(ToolResponse<string?>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        [McpServerTool, Description("Set assigned ProDiag FB for a DB member or tag member")]
        public string blocks_prodiag_assigned_set(
            [Description("Device name")] string deviceName,
            [Description("Path to member (e.g., 'DB_Name.SimpleUdtInstance' or 'ArrayUdtMember.ArrayUdtMember[0]')")] string memberPath,
            [Description("Member type: 'db' for DB member, 'tag' for tag table member")] string memberType,
            [Description("ProDiag FB name to assign")] string proDiagFbName,
            [Description("If true, only validate without making changes")] bool dryRun = false)
        {
            _logger.LogInformation("blocks_prodiag_assigned_set called with deviceName='{DeviceName}', memberPath='{MemberPath}', memberType='{MemberType}', proDiagFbName='{ProDiagFbName}', dryRun={DryRun}", deviceName, memberPath, memberType, proDiagFbName, dryRun);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                IEngineeringObject? member = null;
                if (memberType.ToLowerInvariant() == "db")
                {
                    var dataBlock = plcSoftware.BlockGroup.Blocks.Find(memberPath.Split('.')[0]);
                    if (dataBlock == null)
                    {
                        return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.BlockNotFound, $"DB '{memberPath.Split('.')[0]}' not found"));
                    }

                    // Try to get interface from the block
                    var blockInterface = dataBlock.GetType().GetProperty("Interface")?.GetValue(dataBlock);
                    if (blockInterface == null)
                    {
                        return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Block does not have an accessible interface"));
                    }

                    var members = blockInterface.GetType().GetProperty("Members")?.GetValue(blockInterface) as MemberComposition;
                    if (members == null)
                    {
                        return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Block interface does not have members"));
                    }

                    member = FindMemberByPath(members, memberPath.Substring(memberPath.IndexOf('.') + 1));
                }
                else if (memberType.ToLowerInvariant() == "tag")
                {
                    var tagTable = plcSoftware.TagTableGroup.TagTables.Find(memberPath.Split('.')[0]);
                    if (tagTable == null)
                    {
                        return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.InvalidParameter, $"Tag table '{memberPath.Split('.')[0]}' not found"));
                    }

                    var tag = tagTable.Tags.Find(memberPath.Substring(memberPath.IndexOf('.') + 1));
                    member = tag;
                }
                else
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.InvalidParameter, "memberType must be 'db' or 'tag'"));
                }

                if (member == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.InvalidParameter, $"Member '{memberPath}' not found"));
                }

                var result = _blocksAdapter.SetAssignedProDiagFb(member, proDiagFbName, dryRun);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_prodiag_assigned_set");
                return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        [McpServerTool, Description("Get ProDiag attributes for a ProDiag FB")]
        public string blocks_prodiag_attributes_get(
            [Description("Device name")] string deviceName,
            [Description("ProDiag FB name")] string fbName)
        {
            _logger.LogInformation("blocks_prodiag_attributes_get called with deviceName='{DeviceName}', fbName='{FbName}'", deviceName, fbName);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<ProDiagAttributes>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<ProDiagAttributes>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<ProDiagAttributes>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                var fb = plcSoftware.BlockGroup.Blocks.Find(fbName) as FB;
                if (fb == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<ProDiagAttributes>.CreateError(ErrorCodes.BlockNotFound, $"FB '{fbName}' not found"));
                }

                var result = _blocksAdapter.GetProDiagAttributes(fb);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_prodiag_attributes_get");
                return JsonConvert.SerializeObject(ToolResponse<ProDiagAttributes>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        [McpServerTool, Description("Export ProDiag alarm messages to CSV")]
        public string blocks_prodiag_export_csv(
            [Description("Device name")] string deviceName,
            [Description("ProDiag FB name")] string fbName,
            [Description("Directory path to export CSV files")] string directoryPath)
        {
            _logger.LogInformation("blocks_prodiag_export_csv called with deviceName='{DeviceName}', fbName='{FbName}', directoryPath='{DirectoryPath}'", deviceName, fbName, directoryPath);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                var fb = plcSoftware.BlockGroup.Blocks.Find(fbName) as FB;
                if (fb == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.BlockNotFound, $"FB '{fbName}' not found"));
                }

                var directory = new DirectoryInfo(directoryPath);
                if (!directory.Exists)
                {
                    return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.InvalidParameter, $"Directory '{directoryPath}' does not exist"));
                }

                var result = _blocksAdapter.ExportProDiagCsv(fb, directory);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_prodiag_export_csv");
                return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        #endregion

        #region External Source Tools

        [McpServerTool, Description("Add an external source file to PLC")]
        public string blocks_external_source_add(
            [Description("Device name")] string deviceName,
            [Description("Name for the external source")] string name,
            [Description("Path to the source file (.awl, .scl, .db, .udt)")] string filePath)
        {
            _logger.LogInformation("blocks_external_source_add called with deviceName='{DeviceName}', name='{Name}', filePath='{FilePath}'", deviceName, name, filePath);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<PlcExternalSource>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<PlcExternalSource>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<PlcExternalSource>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                var result = _blocksAdapter.AddExternalSource(plcSoftware, name, filePath);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_external_source_add");
                return JsonConvert.SerializeObject(ToolResponse<PlcExternalSource>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        [McpServerTool, Description("Generate source from blocks or types")]
        public string blocks_source_generate(
            [Description("Device name")] string deviceName,
            [Description("Block or type names to generate source from")] string[] sourceNames,
            [Description("Target file path")] string targetFilePath,
            [Description("Generate options: 'None' or 'WithDependencies'")] string options = "WithDependencies")
        {
            _logger.LogInformation("blocks_source_generate called with deviceName='{DeviceName}', sourceNames=[{SourceNames}], targetFilePath='{TargetFilePath}', options='{Options}'", deviceName, string.Join(",", sourceNames), targetFilePath, options);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                var sources = new List<IGenerateSource>();
                foreach (var name in sourceNames)
                {
                    var block = plcSoftware.BlockGroup.Blocks.Find(name);
                    if (block != null)
                    {
                        sources.Add(block);
                        continue;
                    }

                    var type = plcSoftware.TypeGroup.Types.Find(name);
                    if (type != null)
                    {
                        sources.Add(type);
                        continue;
                    }

                    return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.InvalidParameter, $"Source '{name}' not found as block or type"));
                }

                var targetFile = new FileInfo(targetFilePath);
                var generateOptions = options == "WithDependencies" ? GenerateOptions.WithDependencies : GenerateOptions.None;

                var result = _blocksAdapter.GenerateSource(plcSoftware, sources, targetFile, generateOptions);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_source_generate");
                return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        [McpServerTool, Description("Generate blocks from all external sources")]
        public string blocks_external_source_generate_all(
            [Description("Device name")] string deviceName)
        {
            _logger.LogInformation("blocks_external_source_generate_all called with deviceName='{DeviceName}'", deviceName);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                var results = new List<IEngineeringObject>();
                foreach (var externalSource in plcSoftware.ExternalSourceGroup.ExternalSources)
                {
                    var result = _blocksAdapter.GenerateBlocksFromSource(externalSource, GenerateBlockOption.None);
                    if (!result.Success)
                    {
                        return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(result.ErrorCode ?? ErrorCodes.TiaError, result.Error ?? "Failed to generate blocks", result.Details));
                    }
                    results.AddRange(result.Data);
                }

                return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateSuccess(results));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_external_source_generate_all");
                return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        [McpServerTool, Description("Generate blocks from external source with options")]
        public string blocks_external_source_generate_with_options(
            [Description("Device name")] string deviceName,
            [Description("External source name")] string sourceName,
            [Description("Generate options: 'None' or 'KeepOnError'")] string options = "None")
        {
            _logger.LogInformation("blocks_external_source_generate_with_options called with deviceName='{DeviceName}', sourceName='{SourceName}', options='{Options}'", deviceName, sourceName, options);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                var externalSource = plcSoftware.ExternalSourceGroup.ExternalSources.Find(sourceName);
                if (externalSource == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.InvalidParameter, $"External source '{sourceName}' not found"));
                }

                var generateOptions = options == "KeepOnError" ? GenerateBlockOption.KeepOnError : GenerateBlockOption.None;
                var result = _blocksAdapter.GenerateBlocksFromSource(externalSource, generateOptions);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_external_source_generate_with_options");
                return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        [McpServerTool, Description("Delete an external source file")]
        public string blocks_external_source_delete(
            [Description("Device name")] string deviceName,
            [Description("External source name")] string sourceName,
            [Description("If true, only validate without deleting")] bool dryRun = false)
        {
            _logger.LogInformation("blocks_external_source_delete called with deviceName='{DeviceName}', sourceName='{SourceName}', dryRun={DryRun}", deviceName, sourceName, dryRun);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                var externalSource = plcSoftware.ExternalSourceGroup.ExternalSources.Find(sourceName);
                if (externalSource == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.InvalidParameter, $"External source '{sourceName}' not found"));
                }

                var result = _blocksAdapter.DeleteExternalSource(externalSource, dryRun);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_external_source_delete");
                return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        #endregion

        #region Block Management Tools

        [McpServerTool, Description("Delete a user data type")]
        public string blocks_udt_delete(
            [Description("Device name")] string deviceName,
            [Description("UDT name")] string udtName,
            [Description("If true, only validate without deleting")] bool dryRun = false)
        {
            _logger.LogInformation("blocks_udt_delete called with deviceName='{DeviceName}', udtName='{UdtName}', dryRun={DryRun}", deviceName, udtName, dryRun);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                var udt = plcSoftware.TypeGroup.Types.Find(udtName);
                if (udt == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.InvalidParameter, $"UDT '{udtName}' not found"));
                }

                var result = _blocksAdapter.DeleteUdt(udt, dryRun);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_udt_delete");
                return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        [McpServerTool, Description("List PLC system data types")]
        public string blocks_system_types_list(
            [Description("Device name")] string deviceName)
        {
            _logger.LogInformation("blocks_system_types_list called with deviceName='{DeviceName}'", deviceName);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<PlcSystemTypeGroup>>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<PlcSystemTypeGroup>>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<PlcSystemTypeGroup>>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                var result = _blocksAdapter.GetSystemTypes(plcSoftware);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_system_types_list");
                return JsonConvert.SerializeObject(ToolResponse<IList<PlcSystemTypeGroup>>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        [McpServerTool, Description("Open a block in the TIA Portal editor")]
        public string blocks_editor_open_block(
            [Description("Device name")] string deviceName,
            [Description("Block name")] string blockName)
        {
            _logger.LogInformation("blocks_editor_open_block called with deviceName='{DeviceName}', blockName='{BlockName}'", deviceName, blockName);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                var block = plcSoftware.BlockGroup.Blocks.Find(blockName);
                if (block == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.BlockNotFound, $"Block '{blockName}' not found"));
                }

                var result = _blocksAdapter.OpenBlockInEditor(block);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_editor_open_block");
                return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        [McpServerTool, Description("Open a type in the TIA Portal editor")]
        public string blocks_editor_open_type(
            [Description("Device name")] string deviceName,
            [Description("Type name")] string typeName)
        {
            _logger.LogInformation("blocks_editor_open_type called with deviceName='{DeviceName}', typeName='{TypeName}'", deviceName, typeName);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                var type = plcSoftware.TypeGroup.Types.Find(typeName);
                if (type == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.InvalidParameter, $"Type '{typeName}' not found"));
                }

                var result = _blocksAdapter.OpenTypeInEditor(type);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_editor_open_type");
                return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        [McpServerTool, Description("Get fingerprints for a block or UDT")]
        public string blocks_fingerprints_get(
            [Description("Device name")] string deviceName,
            [Description("Block or UDT name")] string name)
        {
            _logger.LogInformation("blocks_fingerprints_get called with deviceName='{DeviceName}', name='{Name}'", deviceName, name);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<Fingerprint>>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<Fingerprint>>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<Fingerprint>>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                IEngineeringObject? obj = plcSoftware.BlockGroup.Blocks.Find(name);
                if (obj == null)
                {
                    obj = plcSoftware.TypeGroup.Types.Find(name);
                }

                if (obj == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<Fingerprint>>.CreateError(ErrorCodes.InvalidParameter, $"Block or UDT '{name}' not found"));
                }

                var result = _blocksAdapter.GetFingerprints(obj);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_fingerprints_get");
                return JsonConvert.SerializeObject(ToolResponse<IList<Fingerprint>>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        [McpServerTool, Description("Set priority for an OB block")]
        public string blocks_ob_priority_set(
            [Description("Device name")] string deviceName,
            [Description("OB block name")] string obName,
            [Description("Priority number")] int priority,
            [Description("If true, only validate without setting")] bool dryRun = false)
        {
            _logger.LogInformation("blocks_ob_priority_set called with deviceName='{DeviceName}', obName='{ObName}', priority={Priority}, dryRun={DryRun}", deviceName, obName, priority, dryRun);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                var obBlock = plcSoftware.BlockGroup.Blocks.Find(obName);
                if (obBlock == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.BlockNotFound, $"OB block '{obName}' not found"));
                }

                var result = _blocksAdapter.SetObPriority(obBlock, priority, dryRun);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_ob_priority_set");
                return JsonConvert.SerializeObject(ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        #endregion

        #region Webserver Pages Tools

        // [McpServerTool, Description("Generate blocks for webserver user-defined pages")]
        // public string blocks_webserver_pages_generate_blocks(
        //     [Description("Device name")] string deviceName,
        //     [Description("Generate options: 'None' or 'Override'")] string options = "None",
        //     [Description("HTML directory path (optional)")] string? htmlDirectory = null,
        //     [Description("Default HTML page path (optional)")] string? defaultHtmlPage = null,
        //     [Description("Application name (optional)")] string? applicationName = null)
        // {
        //     _logger.LogInformation("blocks_webserver_pages_generate_blocks called with deviceName='{DeviceName}', options='{Options}'", deviceName, options);

        //     try
        //     {
        //         var project = _sessionManager.CurrentProject;
        //         if (project == null)
        //         {
        //             return JsonConvert.SerializeObject(ToolResponse<IList<PlcBlock>>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
        //         }

        //         var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
        //         if (device == null)
        //         {
        //             return JsonConvert.SerializeObject(ToolResponse<IList<PlcBlock>>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
        //         }

        //         var deviceItem = device.DeviceItems.FirstOrDefault();
        //         if (deviceItem == null)
        //         {
        //             return JsonConvert.SerializeObject(ToolResponse<IList<PlcBlock>>.CreateError(ErrorCodes.TiaError, "No device item found"));
        //         }

        //         var generateOptions = options == "Override" ? WebDBGenerateOptions.Override : WebDBGenerateOptions.None;
        //         DirectoryInfo? htmlDir = htmlDirectory != null ? new DirectoryInfo(htmlDirectory) : null;
        //         FileInfo? defaultPage = defaultHtmlPage != null ? new FileInfo(defaultHtmlPage) : null;

        //         var result = _blocksAdapter.GenerateWebserverPagesBlocks(deviceItem, generateOptions, htmlDir, defaultPage, applicationName);
        //         return JsonConvert.SerializeObject(result);
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Unexpected error in blocks_webserver_pages_generate_blocks");
        //         return JsonConvert.SerializeObject(ToolResponse<IList<PlcBlock>>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
        //     }
        // }

        #endregion

        #region Loadable Files Tools

        // [McpServerTool, Description("Generate loadable file for blocks")]
        // public string blocks_loadable_generate(
        //     [Description("Device name")] string deviceName,
        //     [Description("Block names to include")] string[] blockNames,
        //     [Description("Output file path")] string outputFilePath,
        //     [Description("Target option: 'Plc' or 'PlcSim'")] string targetOption = "Plc",
        //     [Description("If true, only validate without generating")] bool dryRun = false)
        // {
        //     _logger.LogInformation("blocks_loadable_generate called with deviceName='{DeviceName}', blockNames=[{BlockNames}], outputFilePath='{OutputFilePath}', targetOption='{TargetOption}', dryRun={DryRun}", deviceName, string.Join(",", blockNames), outputFilePath, targetOption, dryRun);

        //     try
        //     {
        //         var project = _sessionManager.CurrentProject;
        //         if (project == null)
        //         {
        //             return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
        //         }

        //         var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
        //         if (device == null)
        //         {
        //             return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
        //         }

        //         var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
        //         if (plcSoftware == null)
        //         {
        //             return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
        //         }

        //         var blocks = new List<PlcBlock>();
        //         foreach (var name in blockNames)
        //         {
        //             var block = plcSoftware.BlockGroup.Blocks.Find(name);
        //             if (block == null)
        //             {
        //                 return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.BlockNotFound, $"Block '{name}' not found"));
        //             }
        //             blocks.Add(block);
        //         }

        //         var target = targetOption == "PlcSim" ? TargetOption.PlcSim : TargetOption.Plc;
        //         var outputFile = new FileInfo(outputFilePath);

        //         var result = _blocksAdapter.GenerateLoadableFile(plcSoftware, outputFile, blocks, target, dryRun);
        //         return JsonConvert.SerializeObject(result);
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Unexpected error in blocks_loadable_generate");
        //         return JsonConvert.SerializeObject(ToolResponse<string>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
        //     }
        // }

        #endregion

        #region Group-based Generation Tools

        [McpServerTool, Description("Generate blocks from external source in specific user group")]
        public string blocks_external_source_generate_in_group(
            [Description("Device name")] string deviceName,
            [Description("External source name")] string sourceName,
            [Description("Block group name")] string groupName,
            [Description("Generate options: 'None' or 'KeepOnError'")] string options = "None")
        {
            _logger.LogInformation("blocks_external_source_generate_in_group called with deviceName='{DeviceName}', sourceName='{SourceName}', groupName='{GroupName}', options='{Options}'", deviceName, sourceName, groupName, options);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.NoProject, "No project is currently open"));
                }

                var device = project.Devices.FirstOrDefault(d => d.Name == deviceName);
                if (device == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.DeviceNotFound, $"Device '{deviceName}' not found"));
                }

                var plcSoftware = _sessionManager.PortalService.GetPlcSoftware(device);
                if (plcSoftware == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.TiaError, $"Device '{deviceName}' does not have PLC software"));
                }

                var externalSource = plcSoftware.ExternalSourceGroup.ExternalSources.Find(sourceName);
                if (externalSource == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.InvalidParameter, $"External source '{sourceName}' not found"));
                }

                var blockGroup = plcSoftware.BlockGroup.Groups.Find(groupName);
                if (blockGroup == null)
                {
                    return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.InvalidParameter, $"Block group '{groupName}' not found"));
                }

                var generateOptions = options == "KeepOnError" ? GenerateBlockOption.KeepOnError : GenerateBlockOption.None;
                var result = _blocksAdapter.GenerateBlocksFromSourceInGroup(externalSource, blockGroup, generateOptions);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in blocks_external_source_generate_in_group");
                return JsonConvert.SerializeObject(ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.TiaError, "Unexpected error", ex.Message));
            }
        }

        #endregion

        #region Helper Methods

        private Member? FindMemberByPath(MemberComposition members, string path)
        {
            var parts = path.Split('.');
            Member? current = null;

            foreach (var part in parts)
            {
                if (current == null)
                {
                    current = members.Find(part);
                }
                else
                {
                    // Check if current member has a Members property that is a MemberComposition
                    var membersProperty = current.GetType().GetProperty("Members");
                    if (membersProperty != null)
                    {
                        var subMembers = membersProperty.GetValue(current) as MemberComposition;
                        if (subMembers != null)
                        {
                            current = subMembers.Find(part);
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }

                if (current == null)
                {
                    return null;
                }
            }

            return current;
        }

        #endregion
    }
}