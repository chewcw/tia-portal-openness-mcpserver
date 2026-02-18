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
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public class DeviceItemTools
    {
        private readonly ILogger<DeviceItemTools> _logger;
        private readonly TiaPortalSessionManager _sessionManager;

        public DeviceItemTools(
            ILogger<DeviceItemTools> logger,
            TiaPortalSessionManager sessionManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
        }

        [McpServerTool, Description("List all device items in a device")]
        public string deviceitems_list(
            [Description("Device name")] string deviceName)
        {
            _logger.LogInformation("deviceitems_list called with deviceName='{DeviceName}'", deviceName);

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

                var deviceItems = device.DeviceItems.Select(item => new
                {
                    name = item.Name,
                    typeName = item.TypeIdentifier,
                    position = item.PositionNumber
                    // address = item.Address // TODO: Check if Address property exists
                }).ToList();

                _logger.LogInformation("Found {Count} device items in device '{DeviceName}'", deviceItems.Count, deviceName);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        deviceName = deviceName,
                        deviceItemCount = deviceItems.Count,
                        deviceItems = deviceItems
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error listing device items for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error listing device items: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing device items for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error listing device items: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Get attributes of a device item")]
        public string deviceitems_get_attributes(
            [Description("Device name")] string deviceName,
            [Description("Device item name")] string deviceItemName)
        {
            _logger.LogInformation("deviceitems_get_attributes called with deviceName='{DeviceName}', deviceItemName='{DeviceItemName}'", deviceName, deviceItemName);

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

                var deviceItem = device.DeviceItems.FirstOrDefault(di => di.Name == deviceItemName);
                if (deviceItem == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.DeviceItemNotFound,
                            $"Device item '{deviceItemName}' not found in device '{deviceName}'"
                        )
                    );
                }

                var attributes = new
                {
                    name = deviceItem.Name,
                    typeIdentifier = deviceItem.TypeIdentifier,
                    positionNumber = deviceItem.PositionNumber
                    // address = deviceItem.Address // TODO: Check if Address property exists
                };

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(attributes)
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error getting attributes for device item '{DeviceItemName}' in device '{DeviceName}'", deviceItemName, deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error getting device item attributes: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attributes for device item '{DeviceItemName}' in device '{DeviceName}'", deviceItemName, deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error getting device item attributes: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Search the hardware catalog for device items")]
        public string catalog_search_device_items(
            [Description("Search query (e.g., 'CPU', '6ES7', order number)")] string query,
            [Description("Maximum number of results to return")] int maxResults = 10)
        {
            _logger.LogInformation("catalog_search_device_items called with query='{Query}', maxResults={MaxResults}", query, maxResults);

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

                // TODO: Implement catalog search
                // var catalog = project.GetCatalog();
                // var results = catalog.Search(query, maxResults);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.NotImplemented,
                        "Hardware catalog search not implemented yet."
                    )
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error searching hardware catalog with query '{Query}'", query);
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
                _logger.LogError(ex, "Error searching hardware catalog with query '{Query}'", query);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error searching catalog: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }
        public string deviceitems_plug_new(
            [Description("Device name")] string deviceName,
            [Description("Device item order number (e.g., '6ES7 315-2AG10-0AB0')")] string orderNumber,
            [Description("Position number for the device item")] int position,
            [Description("Address for the device item (optional)")] string? address = null,
            [Description("Whether to perform a dry run (true = validate only, false = plug device item)")] bool dryRun = false)
        {
            _logger.LogInformation("deviceitems_plug_new called with deviceName='{DeviceName}', orderNumber='{OrderNumber}', position={Position}, address='{Address}', dryRun={DryRun}", deviceName, orderNumber, position, address, dryRun);

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
                            orderNumber = orderNumber,
                            position = position,
                            address = address,
                            message = "Dry run successful: Device item plugging validation not fully implemented",
                            dryRun = true
                        })
                    );
                }

                // TODO: Implement actual plugging using TIA API
                // var deviceItem = device.PlugNew(orderNumber, position, address);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.NotImplemented,
                        "Device item plugging requires hardware catalog integration. Use dry-run mode to validate parameters."
                    )
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error plugging device item into device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error plugging device item: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error plugging device item into device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error plugging device item: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Move a device item to a new position")]
        public string deviceitems_plug_move(
            [Description("Device name")] string deviceName,
            [Description("Device item name")] string deviceItemName,
            [Description("New position number")] int newPosition,
            [Description("New address (optional)")] string? newAddress = null,
            [Description("Whether to perform a dry run (true = validate only, false = move device item)")] bool dryRun = false)
        {
            _logger.LogInformation("deviceitems_plug_move called with deviceName='{DeviceName}', deviceItemName='{DeviceItemName}', newPosition={NewPosition}, newAddress='{NewAddress}', dryRun={DryRun}", deviceName, deviceItemName, newPosition, newAddress, dryRun);

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

                var deviceItem = device.DeviceItems.FirstOrDefault(di => di.Name == deviceItemName);
                if (deviceItem == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.DeviceItemNotFound,
                            $"Device item '{deviceItemName}' not found in device '{deviceName}'"
                        )
                    );
                }

                if (dryRun)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateSuccess(new
                        {
                            deviceName = deviceName,
                            deviceItemName = deviceItemName,
                            newPosition = newPosition,
                            newAddress = newAddress,
                            message = "Dry run successful: Device item move validation not fully implemented",
                            dryRun = true
                        })
                    );
                }

                // TODO: Implement actual move using TIA API
                // deviceItem.MoveTo(newPosition, newAddress);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.NotImplemented,
                        "Device item moving not implemented yet."
                    )
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error moving device item '{DeviceItemName}' in device '{DeviceName}'", deviceItemName, deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error moving device item: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving device item '{DeviceItemName}' in device '{DeviceName}'", deviceItemName, deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error moving device item: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Copy a device item to a new position")]
        public string deviceitems_copy(
            [Description("Device name")] string deviceName,
            [Description("Device item name to copy")] string sourceDeviceItemName,
            [Description("New position number")] int newPosition,
            [Description("New address (optional)")] string? newAddress = null,
            [Description("Whether to perform a dry run (true = validate only, false = copy device item)")] bool dryRun = false)
        {
            _logger.LogInformation("deviceitems_copy called with deviceName='{DeviceName}', sourceDeviceItemName='{SourceDeviceItemName}', newPosition={NewPosition}, newAddress='{NewAddress}', dryRun={DryRun}", deviceName, sourceDeviceItemName, newPosition, newAddress, dryRun);

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

                var sourceDeviceItem = device.DeviceItems.FirstOrDefault(di => di.Name == sourceDeviceItemName);
                if (sourceDeviceItem == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.DeviceItemNotFound,
                            $"Source device item '{sourceDeviceItemName}' not found in device '{deviceName}'"
                        )
                    );
                }

                if (dryRun)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateSuccess(new
                        {
                            deviceName = deviceName,
                            sourceDeviceItemName = sourceDeviceItemName,
                            newPosition = newPosition,
                            newAddress = newAddress,
                            message = "Dry run successful: Device item copy validation not fully implemented",
                            dryRun = true
                        })
                    );
                }

                // TODO: Implement actual copy using TIA API
                // var newDeviceItem = sourceDeviceItem.CopyTo(newPosition, newAddress);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.NotImplemented,
                        "Device item copying not implemented yet."
                    )
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error copying device item '{SourceDeviceItemName}' in device '{DeviceName}'", sourceDeviceItemName, deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error copying device item: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying device item '{SourceDeviceItemName}' in device '{DeviceName}'", sourceDeviceItemName, deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error copying device item: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Delete a device item from a device")]
        public string deviceitems_delete(
            [Description("Device name")] string deviceName,
            [Description("Device item name")] string deviceItemName,
            [Description("Whether to perform a dry run (true = validate only, false = delete device item)")] bool dryRun = false)
        {
            _logger.LogInformation("deviceitems_delete called with deviceName='{DeviceName}', deviceItemName='{DeviceItemName}', dryRun={DryRun}", deviceName, deviceItemName, dryRun);

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

                var deviceItem = device.DeviceItems.FirstOrDefault(di => di.Name == deviceItemName);
                if (deviceItem == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.DeviceItemNotFound,
                            $"Device item '{deviceItemName}' not found in device '{deviceName}'"
                        )
                    );
                }

                if (dryRun)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateSuccess(new
                        {
                            deviceName = deviceName,
                            deviceItemName = deviceItemName,
                            message = "Dry run successful: Device item deletion validation not fully implemented",
                            dryRun = true
                        })
                    );
                }

                // TODO: Implement actual delete using TIA API
                // deviceItem.Delete();
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.NotImplemented,
                        "Device item deletion not implemented yet."
                    )
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error deleting device item '{DeviceItemName}' from device '{DeviceName}'", deviceItemName, deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error deleting device item: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting device item '{DeviceItemName}' from device '{DeviceName}'", deviceItemName, deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error deleting device item: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }
    }
}