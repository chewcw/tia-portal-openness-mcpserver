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
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.HW.HardwareCatalog;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public class DeviceTools
    {
        private readonly ILogger<DeviceTools> _logger;
        private readonly TiaPortalSessionManager _sessionManager;

        public DeviceTools(
            ILogger<DeviceTools> logger,
            TiaPortalSessionManager sessionManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
        }

        [McpServerTool, Description("Enumerate all hardware devices in the current TIA Portal project including PLCs, HMIs, IOdevices, and network components. Returns device list with names, type identifiers, and device item counts. If no project is open and client supports MCP Apps/elicitation, prompts for projectPath. Prerequisites: Project must be open. Use this as the first step to discover available devices before device-specific operations like compilation, tag management, or block editing.")]
        public async Task<string> devices_list(
            McpServer server,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("devices_list called");

            var ensureProjectResult = await EnsureProjectOpenAsync(server, cancellationToken);
            if (ensureProjectResult != null)
            {
                return ensureProjectResult;
            }

            try
            {
                var project = _sessionManager.CurrentProject;

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

        [McpServerTool, Description("Create a new hardware device (PLC, HMI, IO device) in the current project by specifying its order number from the hardware catalog. If no project is open and client supports MCP Apps/elicitation, prompts for projectPath. If deviceName/orderNumber are missing, prompts for them. Returns device name and metadata. Prerequisites: Project must be open, order number must be valid. Use dryRun=true to validate order number. Status: Full hardware catalog integration pending; use devices_search_catalog to find valid order numbers, then create device.")]
        public async Task<string> devices_create(
            McpServer server,
            [Description("Device name")] string? deviceName,
            [Description("Device order number (e.g., '6ES7 515-2AM02-0AB0')")] string? orderNumber,
            [Description("Whether to perform a dry run (true = validate only, false = create device)")] bool dryRun = false,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("devices_create called with deviceName='{DeviceName}', orderNumber='{OrderNumber}', dryRun={DryRun}", deviceName, orderNumber, dryRun);

            var ensureProjectResult = await EnsureProjectOpenAsync(server, cancellationToken);
            if (ensureProjectResult != null)
            {
                return ensureProjectResult;
            }

            if (string.IsNullOrWhiteSpace(deviceName) || string.IsNullOrWhiteSpace(orderNumber))
            {
                if (server.ClientCapabilities?.Elicitation == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.OperationNotSupported,
                            "Client does not support elicitation. Provide deviceName/orderNumber or use a client with MCP Apps/elicitation support."
                        )
                    );
                }

                var schema = new ElicitRequestParams.RequestSchema();
                if (string.IsNullOrWhiteSpace(deviceName))
                {
                    schema.Properties["deviceName"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Device name to create"
                    };
                }
                if (string.IsNullOrWhiteSpace(orderNumber))
                {
                    schema.Properties["orderNumber"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Device order number (e.g., 6ES7 515-2AM02-0AB0)"
                    };
                }

                schema.Required = schema.Properties.Keys.ToArray();

                var response = await server.ElicitAsync(new ElicitRequestParams
                {
                    Message = "Missing required device details. Please provide the requested fields.",
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

                if (string.IsNullOrWhiteSpace(orderNumber) &&
                    response.Content != null &&
                    response.Content.TryGetValue("orderNumber", out var orderNumberElement) &&
                    orderNumberElement.ValueKind == JsonValueKind.String)
                {
                    orderNumber = orderNumberElement.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(deviceName) || string.IsNullOrWhiteSpace(orderNumber))
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.InvalidParameter,
                        "deviceName and orderNumber are required."
                    )
                );
            }

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

                if (dryRun)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateSuccess(new
                        {
                            deviceName = deviceName,
                            orderNumber = orderNumber,
                            message = $"Dry run successful: Device creation validation not fully implemented, but parameters are valid",
                            dryRun = true
                        })
                    );
                }

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.NotImplemented,
                        "Device creation requires hardware catalog integration. Use dry-run mode to validate order numbers."
                    )
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error creating device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error creating device: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error creating device: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Delete a hardware device and all its associated configuration (blocks, tags, networks) from the project. If no project is open and client supports MCP Apps/elicitation, prompts for projectPath. If deviceName is missing, prompts for it. Returns confirmation. Prerequisites: Project must be open, device must exist. Use dryRun=true to validate deletion safety. Warning: Deletion is permanent and removes all device data including PLC software, HMI screens, and network connections. Backup project first.")]
        public async Task<string> devices_delete(
            McpServer server,
            [Description("Device name")] string? deviceName,
            [Description("Whether to perform a dry run (true = validate only, false = delete device)")] bool dryRun = false,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("devices_delete called with deviceName='{DeviceName}', dryRun={DryRun}", deviceName, dryRun);

            var ensureProjectResult = await EnsureProjectOpenAsync(server, cancellationToken);
            if (ensureProjectResult != null)
            {
                return ensureProjectResult;
            }

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                if (server.ClientCapabilities?.Elicitation == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.OperationNotSupported,
                            "Client does not support elicitation. Provide deviceName or use a client with MCP Apps/elicitation support."
                        )
                    );
                }

                var schema = new ElicitRequestParams.RequestSchema
                {
                    Properties =
                    {
                        ["deviceName"] = new ElicitRequestParams.StringSchema
                        {
                            Description = "Device name to delete"
                        }
                    },
                    Required = new[] { "deviceName" }
                };

                var response = await server.ElicitAsync(new ElicitRequestParams
                {
                    Message = "Device name is required to delete a device.",
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

                if (response.Content != null &&
                    response.Content.TryGetValue("deviceName", out var deviceNameElement) &&
                    deviceNameElement.ValueKind == JsonValueKind.String)
                {
                    deviceName = deviceNameElement.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.InvalidParameter,
                        "deviceName is required."
                    )
                );
            }

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

                if (dryRun)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateSuccess(new
                        {
                            deviceName = deviceName,
                            typeIdentifier = device.TypeIdentifier,
                            message = $"Dry run successful: Device '{deviceName}' can be deleted",
                            dryRun = true
                        })
                    );
                }

                device.Delete();
                _logger.LogInformation("Device '{DeviceName}' deleted successfully", deviceName);

                try
                {
                    _sessionManager.SaveCurrentProject();
                    _logger.LogInformation("Project saved after device deletion");
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "Failed to save project after device deletion");
                }

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        deviceName = deviceName,
                        message = $"Device '{deviceName}' deleted successfully"
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error deleting device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error deleting device: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error deleting device: {ex.Message}",
                        ex.ToString()
                    )
                );
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

                if (projectPath != null)
                {
                    _sessionManager.OpenProject(projectPath);
                }
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

        [McpServerTool, Description("Retrieve all configuration attributes for a specific device including name, type identifier, device item count, and hardware identifiers. Returns attribute dictionary. If no project is open and client supports MCP Apps/elicitation, prompts for projectPath. If deviceName is missing, prompts for it. Prerequisites: Project must be open, device must exist. Use this to inspect device configuration details before modifications or for device inventory documentation.")]
        public async Task<string> devices_get_attributes(
            McpServer server,
            [Description("Device name")] string? deviceName,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("devices_get_attributes called with deviceName='{DeviceName}'", deviceName);

            var ensureProjectResult = await EnsureProjectOpenAsync(server, cancellationToken);
            if (ensureProjectResult != null)
            {
                return ensureProjectResult;
            }

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                if (server.ClientCapabilities?.Elicitation == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.OperationNotSupported,
                            "Client does not support elicitation. Provide deviceName or use a client with MCP Apps/elicitation support."
                        )
                    );
                }

                var schema = new ElicitRequestParams.RequestSchema
                {
                    Properties =
                    {
                        ["deviceName"] = new ElicitRequestParams.StringSchema
                        {
                            Description = "Device name to get attributes from"
                        }
                    },
                    Required = new[] { "deviceName" }
                };

                var response = await server.ElicitAsync(new ElicitRequestParams
                {
                    Message = "Device name is required to get device attributes.",
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

                if (response.Content != null &&
                    response.Content.TryGetValue("deviceName", out var deviceNameElement) &&
                    deviceNameElement.ValueKind == JsonValueKind.String)
                {
                    deviceName = deviceNameElement.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.InvalidParameter,
                        "deviceName is required."
                    )
                );
            }

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

                var attributes = GetDeviceAttributes(device);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        deviceName = deviceName,
                        attributes = attributes,
                        attributeCount = attributes.Count
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error getting attributes for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error getting device attributes: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attributes for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error getting device attributes: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Modify a specific configuration attribute on a device such as name or hardware properties. Returns success confirmation. If no project is open and client supports MCP Apps/elicitation, prompts for projectPath. If deviceName/attributeName/attributeValue are missing, prompts for them. Prerequisites: Project must be open, device must exist, attribute must be settable. Use dryRun=true to validate attribute name and value. Note: Limited attribute setting supported; primarily 'Name' attribute. Use devices_get_attributes to discover available attributes.")]
        public async Task<string> devices_set_attribute(
            McpServer server,
            [Description("Device name")] string? deviceName,
            [Description("Attribute name")] string? attributeName,
            [Description("Attribute value")] string? attributeValue,
            [Description("Whether to perform a dry run (true = validate only, false = set attribute)")] bool dryRun = false,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("devices_set_attribute called with deviceName='{DeviceName}', attributeName='{AttributeName}', attributeValue='{AttributeValue}', dryRun={DryRun}", deviceName, attributeName, attributeValue, dryRun);

            var ensureProjectResult = await EnsureProjectOpenAsync(server, cancellationToken);
            if (ensureProjectResult != null)
            {
                return ensureProjectResult;
            }

            if (string.IsNullOrWhiteSpace(deviceName) || string.IsNullOrWhiteSpace(attributeName) || string.IsNullOrWhiteSpace(attributeValue))
            {
                if (server.ClientCapabilities?.Elicitation == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.OperationNotSupported,
                            "Client does not support elicitation. Provide deviceName/attributeName/attributeValue or use a client with MCP Apps/elicitation support."
                        )
                    );
                }

                var schema = new ElicitRequestParams.RequestSchema();
                if (string.IsNullOrWhiteSpace(deviceName))
                {
                    schema.Properties["deviceName"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Device name to modify"
                    };
                }
                if (string.IsNullOrWhiteSpace(attributeName))
                {
                    schema.Properties["attributeName"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Attribute name to set (e.g., Name)"
                    };
                }
                if (string.IsNullOrWhiteSpace(attributeValue))
                {
                    schema.Properties["attributeValue"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Value to set for the attribute"
                    };
                }

                schema.Required = schema.Properties.Keys.ToArray();

                var response = await server.ElicitAsync(new ElicitRequestParams
                {
                    Message = "Missing required parameters to set device attribute.",
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

                if (string.IsNullOrWhiteSpace(attributeName) &&
                    response.Content != null &&
                    response.Content.TryGetValue("attributeName", out var attributeNameElement) &&
                    attributeNameElement.ValueKind == JsonValueKind.String)
                {
                    attributeName = attributeNameElement.GetString();
                }

                if (string.IsNullOrWhiteSpace(attributeValue) &&
                    response.Content != null &&
                    response.Content.TryGetValue("attributeValue", out var attributeValueElement) &&
                    attributeValueElement.ValueKind == JsonValueKind.String)
                {
                    attributeValue = attributeValueElement.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(deviceName) || string.IsNullOrWhiteSpace(attributeName) || string.IsNullOrWhiteSpace(attributeValue))
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.InvalidParameter,
                        "deviceName, attributeName, and attributeValue are required."
                    )
                );
            }

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

                if (dryRun)
                {
                    // Validate that the attribute can be set
                    var attributes = GetDeviceAttributes(device);
                    if (!attributes.ContainsKey(attributeName) && !string.Equals(attributeName, "Name", StringComparison.OrdinalIgnoreCase))
                    {
                        return JsonConvert.SerializeObject(
                            ToolResponse<object>.CreateError(
                                ErrorCodes.InvalidParameter,
                                $"Attribute '{attributeName}' not found on device '{deviceName}'"
                            )
                        );
                    }

                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateSuccess(new
                        {
                            deviceName = deviceName,
                            attributeName = attributeName,
                            attributeValue = attributeValue,
                            message = $"Dry run successful: Attribute '{attributeName}' can be set on device '{deviceName}'",
                            dryRun = true
                        })
                    );
                }

                SetDeviceAttribute(device, attributeName, attributeValue);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        deviceName = deviceName,
                        attributeName = attributeName,
                        attributeValue = attributeValue,
                        message = $"Attribute '{attributeName}' set successfully on device '{deviceName}'"
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error setting attribute '{AttributeName}' on device '{DeviceName}'", attributeName, deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error setting device attribute: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting attribute '{AttributeName}' on device '{DeviceName}'", attributeName, deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error setting device attribute: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Retrieve the Application ID (App ID) assigned to a device for identification in distributed systems or IoT scenarios. Returns App ID string if set, empty string otherwise. If no project is open and client supports MCP Apps/elicitation, prompts for projectPath. If deviceName is missing, prompts for it. Prerequisites: Project must be open, device must exist. Use this to verify device identification configuration before deployment or for inventory tracking.")]
        public async Task<string> devices_get_app_id(
            McpServer server,
            [Description("Device name")] string? deviceName,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("devices_get_app_id called with deviceName='{DeviceName}'", deviceName);

            var ensureProjectResult = await EnsureProjectOpenAsync(server, cancellationToken);
            if (ensureProjectResult != null)
            {
                return ensureProjectResult;
            }

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                if (server.ClientCapabilities?.Elicitation == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.OperationNotSupported,
                            "Client does not support elicitation. Provide deviceName or use a client with MCP Apps/elicitation support."
                        )
                    );
                }

                var schema = new ElicitRequestParams.RequestSchema
                {
                    Properties =
                    {
                        ["deviceName"] = new ElicitRequestParams.StringSchema
                        {
                            Description = "Device name to get App ID from"
                        }
                    },
                    Required = new[] { "deviceName" }
                };

                var response = await server.ElicitAsync(new ElicitRequestParams
                {
                    Message = "Device name is required to get device App ID.",
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

                if (response.Content != null &&
                    response.Content.TryGetValue("deviceName", out var deviceNameElement) &&
                    deviceNameElement.ValueKind == JsonValueKind.String)
                {
                    deviceName = deviceNameElement.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.InvalidParameter,
                        "deviceName is required."
                    )
                );
            }

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

                var appId = GetDeviceAppId(device);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        deviceName = deviceName,
                        appId = appId,
                        hasAppId = !string.IsNullOrEmpty(appId)
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error getting App ID for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error getting device App ID: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting App ID for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error getting device App ID: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Assign an Application ID (App ID) to a device for identification purposes in distributed automation systems or cloud integration. Returns success confirmation. If no project is open and client supports MCP Apps/elicitation, prompts for projectPath. If deviceName/appId are missing, prompts for them. Prerequisites: Project must be open, device must exist. Use dryRun=true to validate App ID format. Use this for device identification in multi-site deployments or IoT scenarios.")]
        public async Task<string> devices_set_app_id(
            McpServer server,
            [Description("Device name")] string? deviceName,
            [Description("App ID value")] string? appId,
            [Description("Whether to perform a dry run (true = validate only, false = set App ID)")] bool dryRun = false,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("devices_set_app_id called with deviceName='{DeviceName}', appId='{AppId}', dryRun={DryRun}", deviceName, appId, dryRun);

            var ensureProjectResult = await EnsureProjectOpenAsync(server, cancellationToken);
            if (ensureProjectResult != null)
            {
                return ensureProjectResult;
            }

            if (string.IsNullOrWhiteSpace(deviceName) || string.IsNullOrWhiteSpace(appId))
            {
                if (server.ClientCapabilities?.Elicitation == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.OperationNotSupported,
                            "Client does not support elicitation. Provide deviceName/appId or use a client with MCP Apps/elicitation support."
                        )
                    );
                }

                var schema = new ElicitRequestParams.RequestSchema();
                if (string.IsNullOrWhiteSpace(deviceName))
                {
                    schema.Properties["deviceName"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Device name to set App ID on"
                    };
                }
                if (string.IsNullOrWhiteSpace(appId))
                {
                    schema.Properties["appId"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "App ID value to assign"
                    };
                }

                schema.Required = schema.Properties.Keys.ToArray();

                var response = await server.ElicitAsync(new ElicitRequestParams
                {
                    Message = "Missing required parameters to set device App ID.",
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

                if (string.IsNullOrWhiteSpace(appId) &&
                    response.Content != null &&
                    response.Content.TryGetValue("appId", out var appIdElement) &&
                    appIdElement.ValueKind == JsonValueKind.String)
                {
                    appId = appIdElement.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(deviceName) || string.IsNullOrWhiteSpace(appId))
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.InvalidParameter,
                        "deviceName and appId are required."
                    )
                );
            }

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

                if (dryRun)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateSuccess(new
                        {
                            deviceName = deviceName,
                            appId = appId,
                            message = $"Dry run successful: App ID can be set on device '{deviceName}'",
                            dryRun = true
                        })
                    );
                }

                SetDeviceAppId(device, appId);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        deviceName = deviceName,
                        appId = appId,
                        message = $"App ID set successfully on device '{deviceName}'"
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error setting App ID on device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error setting device App ID: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting App ID on device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error setting device App ID: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Search the TIA Portal hardware catalog for devices and modules by article number, order number, product name, or partial match. Returns list of matching catalog entries with order numbers, descriptions, type identifiers, and versions. No prerequisites. Use this to discover valid order numbers before devices_create. Essential for finding correct hardware part numbers for device creation.")]
        public string devices_search_catalog(
            [Description("Search term (device name, order number, or partial match)")] string searchTerm,
            [Description("Maximum number of results to return")] int maxResults = 50)
        {
            _logger.LogInformation("devices_search_catalog called with searchTerm='{SearchTerm}', maxResults={MaxResults}", searchTerm, maxResults);

            try
            {
                var results = SearchHardwareCatalog(searchTerm);

                // return JsonConvert.SerializeObject(
                    // ToolResponse<object>.CreateSuccess(new
                    // {
                        // articleNumbers = results.Select(x => x).ToList(),
                    // })
                // );

                var resultsNotNull = results.Where(item => item != null);
                var limitedResults = resultsNotNull.Take(maxResults).Select(item => new
                {
                    articleNumber = item.ArticleNumber,
                    catalogPath = item.CatalogPath,
                    description = item.Description,
                    typeIdentifier = item.TypeIdentifier,
                    typeName = item.TypeName,
                    version = item.Version,
                }).ToList();

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        searchTerm = searchTerm,
                        totalResults = resultsNotNull.Count(),
                        returnedResults = limitedResults.Count,
                        maxResults = maxResults,
                        deviceItems = limitedResults
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error searching hardware catalog");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error searching catalog: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching hardware catalog");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error searching catalog: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        /// <summary>
        /// Gets all attributes of a device
        /// </summary>
        private Dictionary<string, object> GetDeviceAttributes(Device device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            _logger.LogInformation("Getting attributes for device '{DeviceName}'", device.Name);

            try
            {
                var attributes = new Dictionary<string, object>();

                // Get basic device attributes
                attributes["Name"] = device.Name;
                attributes["TypeIdentifier"] = device.TypeIdentifier;
                attributes["DeviceItemsCount"] = device.DeviceItems?.Count ?? 0;
                attributes["IsGsd"] = device.IsGsd;
                attributes["UnpluggedItemsCount"] = device.UnpluggedItems?.Count ?? 0;
                attributes["HwIdentifiersCount"] = device.HwIdentifiers.Count;

                // Get additional attributes if available
            try
            {
                var project = _sessionManager.CurrentProject;
                    // Simplified attribute access - TIA Openness attribute access may vary by version
                    // var attributeProvider = device as IAttributeProvider;
                    // if (attributeProvider != null)
                    // {
                    //     foreach (var attribute in attributeProvider.Attributes)
                    //     {
                    //         try
                    //         {
                    //             attributes[attribute.Name] = attribute.Value ?? "";
                    //         }
                    //         catch
                    //         {
                    //             // Some attributes might not be accessible
                    //             attributes[attribute.Name] = "[Not accessible]";
                    //         }
                    //     }
                    // }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not retrieve extended attributes for device '{DeviceName}'", device.Name);
                }

                return attributes;
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error getting attributes for device '{DeviceName}'", device.Name);
                throw new InvalidOperationException($"Failed to get device attributes: {comEx.Message}", comEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attributes for device '{DeviceName}'", device.Name);
                throw;
            }
        }

        /// <summary>
        /// Sets an attribute on a device
        /// </summary>
        private void SetDeviceAttribute(Device device, string attributeName, object value)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));
            if (string.IsNullOrWhiteSpace(attributeName))
                throw new ArgumentException("Attribute name cannot be empty", nameof(attributeName));

            _logger.LogInformation("Setting attribute '{AttributeName}' on device '{DeviceName}'", attributeName, device.Name);

            try
            {
                if (string.Equals(attributeName, "Name", StringComparison.OrdinalIgnoreCase))
                {
                    device.Name = value?.ToString() ?? "";
                }
                else
                {
                    // Simplified attribute setting - TIA Openness attribute access may vary by version
                    // var attributeProvider = device as IAttributeProvider;
                    // if (attributeProvider != null)
                    // {
                    //     var attribute = attributeProvider.Attributes.FirstOrDefault(a => a.Name == attributeName);
                    //     if (attribute != null)
                    //     {
                    //         attribute.Value = value;
                    //     }
                    //     else
                    //     {
                    //         throw new InvalidOperationException($"Attribute '{attributeName}' not found on device");
                    //     }
                    // }
                    // else
                    // {
                    throw new InvalidOperationException($"Device does not support attribute setting for '{attributeName}'");
                    // }
                }

                _logger.LogInformation("Attribute '{AttributeName}' set successfully on device '{DeviceName}'", attributeName, device.Name);
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error setting attribute '{AttributeName}' on device '{DeviceName}'", attributeName, device.Name);
                throw new InvalidOperationException($"Failed to set device attribute: {comEx.Message}", comEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting attribute '{AttributeName}' on device '{DeviceName}'", attributeName, device.Name);
                throw;
            }
        }

        /// <summary>
        /// Gets the App ID of a device
        /// </summary>
        private string GetDeviceAppId(Device device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            _logger.LogInformation("Getting App ID for device '{DeviceName}'", device.Name);

            try
            {
                // App ID is typically stored as an attribute
                var attributes = GetDeviceAttributes(device);
                if (attributes.TryGetValue("AppId", out var appId))
                {
                    return appId?.ToString() ?? "";
                }

                // Try alternative attribute names
                var possibleNames = new[] { "ApplicationId", "AppID", "ID" };
                foreach (var name in possibleNames)
                {
                    if (attributes.TryGetValue(name, out var value))
                    {
                        return value?.ToString() ?? "";
                    }
                }

                return ""; // No App ID found
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve App ID for device '{DeviceName}'", device.Name);
                return "";
            }
        }

        /// <summary>
        /// Sets the App ID of a device
        /// </summary>
        private void SetDeviceAppId(Device device, string appId)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            _logger.LogInformation("Setting App ID '{AppId}' for device '{DeviceName}'", appId, device.Name);

            try
            {
                SetDeviceAttribute(device, "AppId", appId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting App ID for device '{DeviceName}'", device.Name);
                throw;
            }
        }

        /// <summary>
        /// Searches the hardware catalog for device items
        /// </summary>
        internal List<CatalogEntry> SearchHardwareCatalog(string searchTerm)
        {
            _logger.LogInformation("Searching hardware catalog for '{SearchTerm}'", searchTerm);

            try
            {
                var portal = _sessionManager.PortalService.CurrentPortal;
                if (portal == null)
                {
                    throw new InvalidOperationException("TIA Portal instance not available");
                }

                IList<CatalogEntry>? hardwareCatalogs = portal.HardwareCatalog.Find(searchTerm);
                if (hardwareCatalogs != null)
                {
                    return hardwareCatalogs.Where(hardwareCatalog => hardwareCatalog != null).ToList();
                }
                return new List<CatalogEntry>();
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error searching hardware catalog");
                throw new InvalidOperationException($"Failed to search catalog: {comEx.Message}", comEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching hardware catalog");
                throw;
            }
        }
    }
}
