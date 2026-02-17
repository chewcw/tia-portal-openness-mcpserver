using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Blocks.Interface;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Types;
using Siemens.Engineering.SW.Loader;
using TiaPortalMcpServer.Models;

namespace TiaPortalMcpServer.Services
{
    /// <summary>
    /// Adapter for TIA Portal Openness block-related operations
    /// Wraps TIA Openness APIs with error handling and normalization
    /// </summary>
    public class BlocksAdapter
    {
        private readonly ILogger<BlocksAdapter> _logger;

        public BlocksAdapter(ILogger<BlocksAdapter> logger)
        {
            _logger = logger;
        }

        #region ProDiag Operations

        /// <summary>
        /// Get assigned ProDiag FB for a DB or tag member
        /// </summary>
        public ToolResponse<string?> GetAssignedProDiagFb(IEngineeringObject member)
        {
            try
            {
                var value = member.GetAttribute("AssignedProDiagFB");
                return ToolResponse<string?>.CreateSuccess(value?.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get AssignedProDiagFB for member");
                return ToolResponse<string?>.CreateError(ErrorCodes.TiaError, "Failed to get AssignedProDiagFB", ex.Message);
            }
        }

        /// <summary>
        /// Set assigned ProDiag FB for a DB or tag member
        /// </summary>
        public ToolResponse<bool> SetAssignedProDiagFb(IEngineeringObject member, string proDiagFbName, bool dryRun = false)
        {
            try
            {
                if (!dryRun)
                {
                    member.SetAttribute("AssignedProDiagFB", proDiagFbName);
                }
                return ToolResponse<bool>.CreateSuccess(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set AssignedProDiagFB for member");
                return ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Failed to set AssignedProDiagFB", ex.Message);
            }
        }

        /// <summary>
        /// Get ProDiag FB attributes
        /// </summary>
        public ToolResponse<ProDiagAttributes> GetProDiagAttributes(FB fb)
        {
            try
            {
                var attributes = new ProDiagAttributes
                {
                    ProDiagVersion = fb.GetAttribute("ProDiagVersion")?.ToString(),
                    InitialValueAcquisition = Convert.ToBoolean(fb.GetAttribute("InitialValueAcquisition")),
                    UseCentralTimeStamp = Convert.ToBoolean(fb.GetAttribute("UseCentralTimeStamp"))
                };
                return ToolResponse<ProDiagAttributes>.CreateSuccess(attributes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get ProDiag attributes for FB {Name}", fb.Name);
                return ToolResponse<ProDiagAttributes>.CreateError(ErrorCodes.TiaError, "Failed to get ProDiag attributes", ex.Message);
            }
        }

        /// <summary>
        /// Export ProDiag alarm messages to CSV
        /// </summary>
        public ToolResponse<string> ExportProDiagCsv(FB fb, DirectoryInfo directory)
        {
            try
            {
                if (fb.ProgrammingLanguage != ProgrammingLanguage.ProDiag)
                {
                    return ToolResponse<string>.CreateError(ErrorCodes.InvalidParameter, "Block programming language must be ProDiag");
                }

                if (!fb.IsConsistent)
                {
                    return ToolResponse<string>.CreateError(ErrorCodes.TiaError, "ProDiag FB must be consistent");
                }

                fb.ExportProDIAGInfo(directory);
                return ToolResponse<string>.CreateSuccess($"Exported to {directory.FullName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export ProDiag CSV for FB {Name}", fb.Name);
                return ToolResponse<string>.CreateError(ErrorCodes.TiaError, "Failed to export ProDiag CSV", ex.Message);
            }
        }

        #endregion

        #region External Sources

        /// <summary>
        /// Add external source file
        /// </summary>
        public ToolResponse<PlcExternalSource> AddExternalSource(PlcSoftware plcSoftware, string name, string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    return ToolResponse<PlcExternalSource>.CreateError(ErrorCodes.InvalidParameter, "Source file does not exist");
                }

                var extension = fileInfo.Extension.ToLowerInvariant();
                if (!new[] { ".awl", ".scl", ".db", ".udt" }.Contains(extension))
                {
                    return ToolResponse<PlcExternalSource>.CreateError(ErrorCodes.InvalidParameter, "Unsupported file extension. Must be .awl, .scl, .db, or .udt");
                }

                var externalSource = plcSoftware.ExternalSourceGroup.ExternalSources.CreateFromFile(name, filePath);
                return ToolResponse<PlcExternalSource>.CreateSuccess(externalSource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add external source {Name} from {Path}", name, filePath);
                return ToolResponse<PlcExternalSource>.CreateError(ErrorCodes.TiaError, "Failed to add external source", ex.Message);
            }
        }

        /// <summary>
        /// Generate source from blocks or types
        /// </summary>
        public ToolResponse<string> GenerateSource(PlcSoftware plcSoftware, IEnumerable<IGenerateSource> sources, FileInfo targetFile, GenerateOptions options)
        {
            try
            {
                // Get the external source system group from PlcSoftware
                var systemGroup = plcSoftware.GetType().GetProperty("ExternalSourceSystemGroup")?.GetValue(plcSoftware) as PlcExternalSourceSystemGroup;
                if (systemGroup == null)
                {
                    return ToolResponse<string>.CreateError(ErrorCodes.TiaError, "External source system group not available");
                }

                systemGroup.GenerateSource(sources, targetFile, options);
                return ToolResponse<string>.CreateSuccess($"Generated source to {targetFile.FullName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate source");
                return ToolResponse<string>.CreateError(ErrorCodes.TiaError, "Failed to generate source", ex.Message);
            }
        }

        /// <summary>
        /// Generate blocks from external source
        /// </summary>
        public ToolResponse<IList<IEngineeringObject>> GenerateBlocksFromSource(PlcExternalSource externalSource, GenerateBlockOption options)
        {
            try
            {
                var objects = externalSource.GenerateBlocksFromSource(options);
                return ToolResponse<IList<IEngineeringObject>>.CreateSuccess(objects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate blocks from source {Name}", externalSource.Name);
                return ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.TiaError, "Failed to generate blocks from source", ex.Message);
            }
        }

        /// <summary>
        /// Generate blocks from external source in specific group
        /// </summary>
        public ToolResponse<IList<IEngineeringObject>> GenerateBlocksFromSourceInGroup(PlcExternalSource externalSource, PlcBlockUserGroup blockGroup, GenerateBlockOption options)
        {
            try
            {
                var objects = externalSource.GenerateBlocksFromSource(blockGroup, options);
                return ToolResponse<IList<IEngineeringObject>>.CreateSuccess(objects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate blocks from source {Name} in group {Group}", externalSource.Name, blockGroup.Name);
                return ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.TiaError, "Failed to generate blocks from source in group", ex.Message);
            }
        }

        /// <summary>
        /// Generate types from external source in specific group
        /// </summary>
        public ToolResponse<IList<IEngineeringObject>> GenerateTypesFromSourceInGroup(PlcExternalSource externalSource, PlcTypeUserGroup typeGroup, GenerateBlockOption options)
        {
            try
            {
                var objects = externalSource.GenerateBlocksFromSource(typeGroup, options);
                return ToolResponse<IList<IEngineeringObject>>.CreateSuccess(objects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate types from source {Name} in group {Group}", externalSource.Name, typeGroup.Name);
                return ToolResponse<IList<IEngineeringObject>>.CreateError(ErrorCodes.TiaError, "Failed to generate types from source in group", ex.Message);
            }
        }

        /// <summary>
        /// Delete external source
        /// </summary>
        public ToolResponse<bool> DeleteExternalSource(PlcExternalSource externalSource, bool dryRun = false)
        {
            try
            {
                if (!dryRun)
                {
                    externalSource.Delete();
                }
                return ToolResponse<bool>.CreateSuccess(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete external source {Name}", externalSource.Name);
                return ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Failed to delete external source", ex.Message);
            }
        }

        #endregion

        #region Block Operations

        /// <summary>
        /// Delete UDT
        /// </summary>
        public ToolResponse<bool> DeleteUdt(PlcType udt, bool dryRun = false)
        {
            try
            {
                if (!dryRun)
                {
                    udt.Delete();
                }
                return ToolResponse<bool>.CreateSuccess(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete UDT {Name}", udt.Name);
                return ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Failed to delete UDT", ex.Message);
            }
        }

        /// <summary>
        /// Get system data types
        /// </summary>
        public ToolResponse<IList<PlcSystemTypeGroup>> GetSystemTypes(PlcSoftware plcSoftware)
        {
            try
            {
                var systemTypeGroups = plcSoftware.TypeGroup.SystemTypeGroups.ToList();
                return ToolResponse<IList<PlcSystemTypeGroup>>.CreateSuccess(systemTypeGroups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get system types");
                return ToolResponse<IList<PlcSystemTypeGroup>>.CreateError(ErrorCodes.TiaError, "Failed to get system types", ex.Message);
            }
        }

        /// <summary>
        /// Open block in editor
        /// </summary>
        public ToolResponse<bool> OpenBlockInEditor(PlcBlock block)
        {
            try
            {
                block.ShowInEditor();
                return ToolResponse<bool>.CreateSuccess(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open block {Name} in editor", block.Name);
                return ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Failed to open block in editor", ex.Message);
            }
        }

        /// <summary>
        /// Open type in editor
        /// </summary>
        public ToolResponse<bool> OpenTypeInEditor(PlcType type)
        {
            try
            {
                type.ShowInEditor();
                return ToolResponse<bool>.CreateSuccess(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open type {Name} in editor", type.Name);
                return ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Failed to open type in editor", ex.Message);
            }
        }

        /// <summary>
        /// Get fingerprints for block or UDT
        /// </summary>
        public ToolResponse<IList<Fingerprint>> GetFingerprints(IEngineeringObject obj)
        {
            try
            {
                // Try to get the service using reflection since IEngineeringObject may not have GetService directly
                var getServiceMethod = obj.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "GetService" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

                if (getServiceMethod == null)
                {
                    return ToolResponse<IList<Fingerprint>>.CreateError(ErrorCodes.TiaError, "GetService method not available on object");
                }

                var generic = getServiceMethod.MakeGenericMethod(typeof(FingerprintProvider));
                var provider = generic.Invoke(obj, null) as FingerprintProvider;

                if (provider == null)
                {
                    return ToolResponse<IList<Fingerprint>>.CreateError(ErrorCodes.TiaError, "Fingerprint provider not available");
                }

                var fingerprints = provider.GetFingerprints();
                return ToolResponse<IList<Fingerprint>>.CreateSuccess(fingerprints);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get fingerprints for {Type}", obj.GetType().Name);
                return ToolResponse<IList<Fingerprint>>.CreateError(ErrorCodes.TiaError, "Failed to get fingerprints", ex.Message);
            }
        }

        /// <summary>
        /// Set OB priority
        /// </summary>
        public ToolResponse<bool> SetObPriority(PlcBlock obBlock, int priority, bool dryRun = false)
        {
            try
            {
                if (!dryRun)
                {
                    obBlock.SetAttribute("PriorityNumber", priority);
                }
                return ToolResponse<bool>.CreateSuccess(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set priority for OB {Name}", obBlock.Name);
                return ToolResponse<bool>.CreateError(ErrorCodes.TiaError, "Failed to set OB priority", ex.Message);
            }
        }

        #endregion

        #region Webserver Pages

        /// <summary>
        /// Generate blocks for webserver user-defined pages
        /// </summary>
        // public ToolResponse<IList<PlcBlock>> GenerateWebserverPagesBlocks(DeviceItem deviceItem, WebDBGenerateOptions options, DirectoryInfo? htmlDirectory = null, FileInfo? defaultHtmlPage = null, string? applicationName = null)
        // {
        //     try
        //     {
        //         var service = deviceItem.GetService<WebserverUserDefinedPages>();
        //         if (service == null)
        //         {
        //             return ToolResponse<IList<PlcBlock>>.CreateError(ErrorCodes.TiaError, "Webserver user-defined pages service not available");
        //         }

        //         IList<PlcBlock> blocks;
        //         if (htmlDirectory != null && defaultHtmlPage != null && applicationName != null)
        //         {
        //             blocks = service.GenerateBlocks(htmlDirectory, defaultHtmlPage, applicationName, options);
        //         }
        //         else
        //         {
        //             blocks = service.GenerateBlocks(options);
        //         }

        //         return ToolResponse<IList<PlcBlock>>.CreateSuccess(blocks);
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Failed to generate webserver pages blocks");
        //         return ToolResponse<IList<PlcBlock>>.CreateError(ErrorCodes.TiaError, "Failed to generate webserver pages blocks", ex.Message);
        //     }
        // }

        #endregion

        #region Loadable Files

        /// <summary>
        /// Generate loadable file for blocks
        /// </summary>
        public ToolResponse<string> GenerateLoadableFile(PlcSoftware plcSoftware, FileInfo filePath, IEnumerable<PlcBlock> blocks, TargetOption targetOption, bool dryRun = false)
        {
            try
            {
                var provider = plcSoftware.GetService<LoadableProvider>();
                if (provider == null)
                {
                    return ToolResponse<string>.CreateError(ErrorCodes.TiaError, "Loadable provider not available for this PLC");
                }

                if (!dryRun)
                {
                    provider.GenerateLoadable(filePath, blocks, targetOption);
                }
                return ToolResponse<string>.CreateSuccess($"Loadable file would be generated at {filePath.FullName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate loadable file");
                return ToolResponse<string>.CreateError(ErrorCodes.TiaError, "Failed to generate loadable file", ex.Message);
            }
        }

        #endregion
    }

    /// <summary>
    /// ProDiag attributes model
    /// </summary>
    public class ProDiagAttributes
    {
        public string? ProDiagVersion { get; set; }
        public bool InitialValueAcquisition { get; set; }
        public bool UseCentralTimeStamp { get; set; }
    }
}