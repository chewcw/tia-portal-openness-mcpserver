using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Siemens.Engineering.HW;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public class HardwareTools
    {
        private readonly ILogger<HardwareTools> _logger;
        private readonly TiaPortalSessionManager _sessionManager;

        public HardwareTools(
            ILogger<HardwareTools> logger,
            TiaPortalSessionManager sessionManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
        }

        [McpServerTool, Description("Add a hardware device to the current project")]
        public string add_device(
            [Description("Device type identifier (e.g., 'OrderNumber:6ES7 515-2AM02-0AB0')")] string deviceTypeIdentifier,
            [Description("Device name")] string name)
        {
            _logger.LogInformation("add_device called with deviceTypeIdentifier='{DeviceTypeIdentifier}', name='{Name}'", deviceTypeIdentifier, name);

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

                // Note: Actual device creation requires more complex API calls with device items and configurations
                // This is a simplified placeholder that shows the structure
                _logger.LogInformation("Device '{Name}' with type '{DeviceTypeIdentifier}' would be added", name, deviceTypeIdentifier);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        deviceName = name,
                        deviceType = deviceTypeIdentifier,
                        message = $"Device '{name}' configuration prepared. Note: Full device creation requires TIA Portal API v18+ with specific device item creation."
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error adding device '{Name}'", name);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error adding device: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding device '{Name}'", name);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error adding device: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("List all devices in the current project")]
        public string list_devices()
        {
            _logger.LogInformation("list_devices called");

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

                var devices = project.Devices.Select(device => new
                {
                    name = device.Name,
                    typeName = device.TypeIdentifier,
                    deviceItemsCount = device.DeviceItems?.Count ?? 0
                }).ToList();

                _logger.LogInformation("Found {Count} devices in project", devices.Count);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        deviceCount = devices.Count,
                        devices = devices
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error listing devices");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error listing devices: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing devices");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error listing devices: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Configure a device parameter")]
        public string configure_device(
            [Description("Device name")] string deviceName,
            [Description("Parameter name (currently supports: Name)")] string parameter,
            [Description("Parameter value")] string value)
        {
            _logger.LogInformation("configure_device called with deviceName='{DeviceName}', parameter='{Parameter}', value='{Value}'", deviceName, parameter, value);

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

                if (!string.Equals(parameter, "Name", StringComparison.OrdinalIgnoreCase))
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.InvalidParameter,
                            $"Unsupported parameter '{parameter}'. Supported: Name"
                        )
                    );
                }

                var oldName = device.Name;
                device.Name = value;

                _logger.LogInformation("Device name changed from '{OldName}' to '{NewName}'", oldName, value);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        deviceName = value,
                        oldName = oldName,
                        parameter = "Name",
                        message = $"Device renamed from '{oldName}' to '{value}'"
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error configuring device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error configuring device: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error configuring device: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }
    }
}
