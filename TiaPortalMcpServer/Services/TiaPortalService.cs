using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;

namespace TiaPortalMcpServer.Services
{
    /// <summary>
    /// Manages the lifecycle of a singleton TIA Portal instance
    /// </summary>
    public class TiaPortalService
    {
        private readonly ILogger<TiaPortalService> _logger;
        private TiaPortal? _tiaPortal;
        private readonly object _lock = new object();
        private bool _disposed = false;

        // closing parantheses for regex characters ommitted, because they are not relevant for regex detection
        private readonly char[] _regexChars = ['.', '^', '$', '*', '+', '?', '(', '[', '{', '\\', '|'];

        public TiaPortalService(ILogger<TiaPortalService> logger)
        {
            _logger = logger;
        }

        public bool IsPortalActive => _tiaPortal != null;

        public TiaPortal? CurrentPortal => _tiaPortal;

        public TiaPortal GetOrCreatePortalInstance()
        {
            lock (_lock)
            {
                if (_tiaPortal == null)
                {
                    _logger.LogInformation("Creating new TIA Portal instance...");
                    try
                    {
                        var processes = TiaPortal.GetProcesses();
                        if (processes.Any())
                        {
                            _tiaPortal = processes.First().Attach();
                        }
                        else
                        {
                            _tiaPortal = new TiaPortal(TiaPortalMode.WithoutUserInterface);
                        }
                    }
                    catch (COMException comEx)
                    {
                        _logger.LogError(comEx, "COM error creating TIA Portal instance");
                        throw new InvalidOperationException(
                            $"Failed to create TIA Portal instance: {comEx.Message}",
                            comEx
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating TIA Portal instance");
                        throw;
                    }
                }

                return _tiaPortal;
            }
        }

        public Project OpenProject(string projectPath)
        {
            lock (_lock)
            {
                var portal = GetOrCreatePortalInstance();

                if (!File.Exists(projectPath))
                {
                    throw new FileNotFoundException($"Project file not found: {projectPath}");
                }

                _logger.LogInformation("Opening project: {ProjectPath}", projectPath);

                try
                {
                    var fileInfo = new FileInfo(projectPath);
                    var project = portal.Projects.Open(fileInfo);
                    _logger.LogInformation("Project opened successfully: {ProjectName}", project.Name);
                    return project;
                }
                catch (COMException comEx)
                {
                    _logger.LogError(comEx, "COM error opening project: {ProjectPath}", projectPath);
                    throw new InvalidOperationException(
                        $"Failed to open project: {comEx.Message}",
                        comEx
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error opening project: {ProjectPath}", projectPath);
                    throw;
                }
            }
        }

        public Project? GetCurrentProject()
        {
            if (_tiaPortal != null)
            {
                var projects = _tiaPortal.Projects;
                return projects.FirstOrDefault();
            }
            return null;
        }

        public PlcSoftware? GetPlcSoftware(Project project, string softwarePath)
        {
            if (project == null)
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(project, softwarePath);
            return softwareContainer;
        }

        public List<PlcBlock> GetBlocks(Project project, string softwarePath, string regexName = "")
        {
            if (project == null)
            {
                return new System.Collections.Generic.List<PlcBlock>();
            }

            var blocks = new System.Collections.Generic.List<PlcBlock>();
            var plcSoftware = GetSoftwareContainer(project, softwarePath);
            if (plcSoftware != null)
            {
                var blockGroup = plcSoftware.BlockGroup;
                if (blockGroup != null)
                {
                    GetRecursiveBlocks(blockGroup, blocks, regexName);
                }
            }

            return blocks;
        }

        private PlcSoftware? GetSoftwareContainer(Project project, string softwarePath)
        {
            // Simplified: assume softwarePath is device name
            var device = GetDevice(project, softwarePath);
            if (device != null)
            {
                var softwareContainerType = device.GetType().Assembly.GetType("Siemens.Engineering.SW.SoftwareContainer");
                if (softwareContainerType != null)
                {
                    foreach (var deviceItem in device.DeviceItems)
                    {
                        var getServiceMethod = deviceItem.GetType().GetMethods()
                            .FirstOrDefault(m => m.Name == "GetService" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

                        if (getServiceMethod != null)
                        {
                            var generic = getServiceMethod.MakeGenericMethod(softwareContainerType);
                            var service = generic.Invoke(deviceItem, null);
                            if (service != null)
                            {
                                var softwareProperty = softwareContainerType.GetProperty("Software");
                                var softwareValue = softwareProperty?.GetValue(service);
                                if (softwareValue is PlcSoftware plcSoftware)
                                {
                                    return plcSoftware;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        private Device? GetDevice(Project project, string devicePath)
        {
            if (project == null)
            {
                return null;
            }

            // Simplified: assume devicePath is device name
            return project.Devices.FirstOrDefault(d => d.Name == devicePath);
        }

        private void GetRecursiveBlocks(PlcBlockGroup blockGroup, List<PlcBlock> blocks, string regexName)
        {
            foreach (PlcBlock block in blockGroup.Blocks)
            {
                if (string.IsNullOrEmpty(regexName) ||
                    (regexName.IndexOfAny(_regexChars) >= 0 && Regex.IsMatch(block.Name, regexName, RegexOptions.IgnoreCase)) ||
                    block.Name.Contains(regexName, StringComparison.OrdinalIgnoreCase))
                {
                    blocks.Add(block);
                }
            }

            foreach (PlcBlockUserGroup userGroup in blockGroup.Groups)
            {
                GetRecursiveBlocksInUserGroup(userGroup, blocks, regexName);
            }
        }

        private void GetRecursiveBlocksInUserGroup(PlcBlockUserGroup userGroup, List<PlcBlock> blocks, string regexName)
        {
            foreach (PlcBlock block in userGroup.Blocks)
            {
                if (string.IsNullOrEmpty(regexName) ||
                    (regexName.IndexOfAny(_regexChars) >= 0 && Regex.IsMatch(block.Name, regexName, RegexOptions.IgnoreCase)) ||
                    block.Name.Contains(regexName, StringComparison.OrdinalIgnoreCase))
                {
                    blocks.Add(block);
                }
            }

            foreach (PlcBlockUserGroup subGroup in userGroup.Groups)
            {
                GetRecursiveBlocksInUserGroup(subGroup, blocks, regexName);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                if (_tiaPortal != null)
                {
                    _logger.LogInformation("Disposing TIA Portal instance...");
                    try
                    {
                        _tiaPortal.Dispose();
                        _logger.LogInformation("TIA Portal instance disposed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing TIA Portal instance");
                    }
                    finally
                    {
                        _tiaPortal = null;
                    }
                }

                _disposed = true;
            }
        }
    }
}
