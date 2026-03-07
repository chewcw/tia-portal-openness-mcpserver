using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;

namespace TiaPortalMcpServer
{
    /// <summary>
    /// MCP Server tool class for HMI target operations.
    /// Provides tools to query, validate, and list HMI targets in TIA Portal projects.
    /// </summary>
    [McpServerToolType]
    public class HmiTargetTools
    {
        private readonly ILogger<HmiTargetTools> _logger;
        private readonly TiaPortalSessionManager _sessionManager;
        private readonly HmiTargetAdapter _hmiTargetAdapter;

        /// <summary>
        /// Initializes a new instance of the HmiTargetTools class.
        /// </summary>
        /// <param name="logger">Logger instance for diagnostic output</param>
        /// <param name="sessionManager">Session manager for project access</param>
        /// <param name="hmiTargetAdapter">Adapter for HMI target operations</param>
        public HmiTargetTools(
            ILogger<HmiTargetTools> logger,
            TiaPortalSessionManager sessionManager,
            HmiTargetAdapter hmiTargetAdapter)
        {
            _logger = logger;
            _sessionManager = sessionManager;
            _hmiTargetAdapter = hmiTargetAdapter;
        }

        /// <summary>
        /// Lists all HMI targets in the current project.
        /// </summary>
        /// <returns>JSON string containing the list of HMI targets with metadata</returns>
        [McpServerTool, Description("Enumerate all HMI targets (HMI panels, comfort panels, WinCC systems) in the current project. Returns list of HMI targets with device names, HMI types, target names, and order numbers. Prerequisites: Project must be open. Use this to discover HMI devices before HMI-specific operations like screen management or tag browsing. Filters devices to include only those with HMI software.")]
        public string hmi_targets_list()
        {
            _logger.LogInformation("hmi_targets_list called");

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.NoProject,
                            "No project is currently open. Use projects_open first."
                        )
                    );
                }

                var hmiTargets = new List<object>();

                // Iterate through all devices to find HMI targets
                foreach (var device in project.Devices)
                {
                    var hmiSoftware = _hmiTargetAdapter.GetHmiTarget(device);
                    if (hmiSoftware != null)
                    {
                        var hmiTargetType = _hmiTargetAdapter.GetHmiTargetType(hmiSoftware);
                        var hmiTargetName = _hmiTargetAdapter.GetHmiTargetName(hmiSoftware);

                        // Try to get order number from device
                        string? orderNumber = null;
                        try
                        {
                            orderNumber = device.TypeIdentifier;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Could not retrieve order number for device '{DeviceName}'", device.Name);
                        }

                        hmiTargets.Add(new
                        {
                            deviceName = device.Name,
                            hmiTargetType = hmiTargetType,
                            hmiTargetName = hmiTargetName,
                            orderNumber = orderNumber
                        });
                    }
                }

                _logger.LogInformation("Found {Count} HMI targets in project", hmiTargets.Count);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        hmiTargetCount = hmiTargets.Count,
                        hmiTargets = hmiTargets
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error listing HMI targets");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error listing HMI targets: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing HMI targets");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error listing HMI targets: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        /// <summary>
        /// Gets HMI target information for a specific device.
        /// </summary>
        /// <param name="deviceName">Name of the device to query</param>
        /// <returns>JSON string containing HMI target details if found</returns>
        [McpServerTool, Description("Retrieve detailed HMI configuration for a specific device including HMI type, targetname, and software properties. Returns HMI target metadata. Prerequisites: Project must be open, device must exist. Returns found=false if device is not an HMI target. Use this to inspect HMI configuration details before screen or tag operations, or to verify HMI device type.")]
        public string hmi_targets_get(
            [Description("Name of the device to query")] string deviceName)
        {
            _logger.LogInformation("hmi_targets_get called with deviceName='{DeviceName}'", deviceName);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.NoProject,
                            "No project is currently open. Use projects_open first."
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

                var hmiSoftware = _hmiTargetAdapter.GetHmiTarget(device);
                if (hmiSoftware == null)
                {
                    _logger.LogInformation("Device '{DeviceName}' does not have an HMI target", deviceName);
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateSuccess(new
                        {
                            found = false,
                            deviceName = deviceName,
                            message = $"Device '{deviceName}' does not have an HMI target"
                        })
                    );
                }

                var hmiTargetType = _hmiTargetAdapter.GetHmiTargetType(hmiSoftware);
                var hmiTargetName = _hmiTargetAdapter.GetHmiTargetName(hmiSoftware);
                var properties = _hmiTargetAdapter.GetHmiTargetProperties(hmiSoftware);

                _logger.LogInformation("Retrieved HMI target for device '{DeviceName}'", deviceName);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        found = true,
                        deviceName = deviceName,
                        hmiTarget = new
                        {
                            type = hmiTargetType,
                            name = hmiTargetName,
                            properties = properties
                        }
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error getting HMI target for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error getting HMI target: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HMI target for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error getting HMI target: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        /// <summary>
        /// Validates whether a device has a valid HMI target.
        /// </summary>
        /// <param name="deviceName">Name of the device to validate</param>
        /// <returns>JSON string containing validation result</returns>
        [McpServerTool, Description("Validate whether a device is configured as an HMI target (panel/WinCC system) versus a PLC-only device. Returns boolean isValid and validation message. Prerequisites: Project must be open, device must exist. Use this before calling HMI-specific tools to avoid errors on non-HMI devices. Essential for conditional workflows that handle both PLC and HMI devices differently.")]
        public string hmi_targets_validate(
            [Description("Name of the device to validate")] string deviceName)
        {
            _logger.LogInformation("hmi_targets_validate called with deviceName='{DeviceName}'", deviceName);

            try
            {
                var project = _sessionManager.CurrentProject;
                if (project == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.NoProject,
                            "No project is currently open. Use projects_open first."
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

                var hmiSoftware = _hmiTargetAdapter.GetHmiTarget(device);
                bool isValid = hmiSoftware != null;

                string message;
                if (isValid)
                {
                    var hmiTargetName = _hmiTargetAdapter.GetHmiTargetName(hmiSoftware);
                    message = $"Device '{deviceName}' has a valid HMI target ({hmiTargetName})";
                    _logger.LogInformation(message);
                }
                else
                {
                    message = $"Device '{deviceName}' does not have a valid HMI target. This may be a PLC device or an unconfigured device.";
                    _logger.LogInformation(message);
                }

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        isValid = isValid,
                        deviceName = deviceName,
                        message = message
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error validating HMI target for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error validating HMI target: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating HMI target for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error validating HMI target: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }
    }
}
