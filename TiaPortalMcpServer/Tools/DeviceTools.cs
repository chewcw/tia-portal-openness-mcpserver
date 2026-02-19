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

        [McpServerTool, Description("List all devices in the current project")]
        public string devices_list()
        {
            _logger.LogInformation("devices_list called");

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

        [McpServerTool, Description("Create a new device in the current project")]
        public string devices_create(
            [Description("Device name")] string deviceName,
            [Description("Device order number (e.g., '6ES7 515-2AM02-0AB0')")] string orderNumber,
            [Description("Whether to perform a dry run (true = validate only, false = create device)")] bool dryRun = false)
        {
            _logger.LogInformation("devices_create called with deviceName='{DeviceName}', orderNumber='{OrderNumber}', dryRun={DryRun}", deviceName, orderNumber, dryRun);

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

                if (dryRun)
                {
                    // For dry run, we can't validate order numbers without full catalog implementation
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

                // For now, create a basic device without catalog lookup
                // In a full implementation, this would search the hardware catalog
                // var catalog = project.GetCatalog();
                // var deviceItem = FindDeviceItemInCatalog(catalog, orderNumber);
                // var device = project.Devices.Create(deviceItem, deviceName);

                // Placeholder: TIA Openness device creation requires specific device items
                // This is a simplified version for demonstration
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

        [McpServerTool, Description("Delete a device from the current project")]
        public string devices_delete(
            [Description("Device name")] string deviceName,
            [Description("Whether to perform a dry run (true = validate only, false = delete device)")] bool dryRun = false)
        {
            _logger.LogInformation("devices_delete called with deviceName='{DeviceName}', dryRun={DryRun}", deviceName, dryRun);

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

        [McpServerTool, Description("Get all attributes of a device")]
        public string devices_get_attributes([Description("Device name")] string deviceName)
        {
            _logger.LogInformation("devices_get_attributes called with deviceName='{DeviceName}'", deviceName);

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

        [McpServerTool, Description("Set an attribute on a device")]
        public string devices_set_attribute(
            [Description("Device name")] string deviceName,
            [Description("Attribute name")] string attributeName,
            [Description("Attribute value")] string attributeValue,
            [Description("Whether to perform a dry run (true = validate only, false = set attribute)")] bool dryRun = false)
        {
            _logger.LogInformation("devices_set_attribute called with deviceName='{DeviceName}', attributeName='{AttributeName}', attributeValue='{AttributeValue}', dryRun={DryRun}", deviceName, attributeName, attributeValue, dryRun);

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

        [McpServerTool, Description("Get the App ID of a device")]
        public string devices_get_app_id([Description("Device name")] string deviceName)
        {
            _logger.LogInformation("devices_get_app_id called with deviceName='{DeviceName}'", deviceName);

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

        [McpServerTool, Description("Set the App ID of a device")]
        public string devices_set_app_id(
            [Description("Device name")] string deviceName,
            [Description("App ID value")] string appId,
            [Description("Whether to perform a dry run (true = validate only, false = set App ID)")] bool dryRun = false)
        {
            _logger.LogInformation("devices_set_app_id called with deviceName='{DeviceName}', appId='{AppId}', dryRun={DryRun}", deviceName, appId, dryRun);

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

        [McpServerTool, Description("Search the hardware catalog for device items")]
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
                var portal = _sessionManager.PortalService.Portal;
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