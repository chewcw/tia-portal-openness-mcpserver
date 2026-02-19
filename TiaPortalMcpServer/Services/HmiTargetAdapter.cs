using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.SW;

namespace TiaPortalMcpServer.Services
{
    /// <summary>
    /// Adapter for TIA Portal Openness HMI target-related operations.
    /// Wraps TIA Openness APIs with error handling and normalization.
    /// HMI targets represent Human-Machine Interface devices that run HMI software.
    /// </summary>
    public class HmiTargetAdapter
    {
        private readonly ILogger<HmiTargetAdapter> _logger;

        public HmiTargetAdapter(ILogger<HmiTargetAdapter> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Attempts to retrieve an HMI target (HMI software) from a device.
        /// HMI software is distinguished from PLC software by device type.
        /// </summary>
        /// <param name="device">The device to check for HMI software</param>
        /// <returns>Software object if HMI software found, null otherwise</returns>
        public Software? GetHmiTarget(Device device)
        {
            if (device == null)
            {
                _logger.LogWarning("Device is null when attempting to get HMI target");
                return null;
            }

            try
            {
                // Use reflection to access SoftwareContainer type
                var softwareContainerType = device.GetType().Assembly.GetType("Siemens.Engineering.SW.SoftwareContainer");
                if (softwareContainerType == null)
                {
                    _logger.LogWarning("SoftwareContainer type not found in TIA Portal API");
                    return null;
                }

                // Iterate through device items to find software
                foreach (var deviceItem in device.DeviceItems)
                {
                    try
                    {
                        // Get the GetService method
                        var getServiceMethod = deviceItem.GetType().GetMethods()
                            .FirstOrDefault(m => m.Name == "GetService" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

                        if (getServiceMethod == null)
                        {
                            continue;
                        }

                        // Call GetService<SoftwareContainer>()
                        var generic = getServiceMethod.MakeGenericMethod(softwareContainerType);
                        var softwareContainerObj = generic.Invoke(deviceItem, null);

                        if (softwareContainerObj == null)
                        {
                            continue;
                        }

                        // Get Software property
                        var softwareProperty = softwareContainerType.GetProperty("Software");
                        var software = softwareProperty?.GetValue(softwareContainerObj) as Software;

                        if (software == null)
                        {
                            continue;
                        }

                        // Check if this is NOT a PlcSoftware - if so, it's HMI software
                        if (!(software is PlcSoftware))
                        {
                            _logger.LogDebug("HMI target software found for device '{DeviceName}' of type '{SoftwareType}'",
                                device.Name, software.GetType().Name);
                            return software;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error checking device item for HMI software");
                        continue;
                    }
                }

                _logger.LogDebug("No HMI target found for device '{DeviceName}'", device.Name);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving HMI target from device '{DeviceName}'", device.Name);
                return null;
            }
        }

        /// <summary>
        /// Checks if a software object is an HMI target (not PLC software).
        /// </summary>
        /// <param name="software">The software object to validate</param>
        /// <returns>True if software is not PLC software (assumed HMI), false otherwise</returns>
        public bool IsHmiTarget(Software software)
        {
            if (software == null)
            {
                return false;
            }

            try
            {
                // HMI software is any software that is not PlcSoftware
                return !(software is PlcSoftware);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if software is HMI target");
                return false;
            }
        }

        /// <summary>
        /// Gets the type identifier for an HMI target software.
        /// </summary>
        /// <param name="hmiSoftware">The HMI software to query</param>
        /// <returns>Type identifier string</returns>
        public string? GetHmiTargetType(Software hmiSoftware)
        {
            if (hmiSoftware == null)
            {
                return null;
            }

            try
            {
                return hmiSoftware.GetType().Name;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HMI software type");
                return null;
            }
        }

        /// <summary>
        /// Gets the name of an HMI target software.
        /// </summary>
        /// <param name="hmiSoftware">The HMI software to query</param>
        /// <returns>HMI software name or null if not available</returns>
        public string? GetHmiTargetName(Software hmiSoftware)
        {
            if (hmiSoftware == null)
            {
                return null;
            }

            try
            {
                return hmiSoftware.Name;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HMI software name");
                return null;
            }
        }

        /// <summary>
        /// Gets available properties and attributes of an HMI target software.
        /// </summary>
        /// <param name="hmiSoftware">The HMI software to query</param>
        /// <returns>Dictionary of property names and type information</returns>
        public Dictionary<string, string> GetHmiTargetProperties(Software hmiSoftware)
        {
            var properties = new Dictionary<string, string>();

            if (hmiSoftware == null)
            {
                return properties;
            }

            try
            {
                var type = hmiSoftware.GetType();
                var properties_info = type.GetProperties();

                foreach (var prop in properties_info)
                {
                    try
                    {
                        var value = prop.GetValue(hmiSoftware);
                        properties[prop.Name] = value?.GetType().Name ?? "null";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not read property '{PropertyName}'", prop.Name);
                        properties[prop.Name] = "Error";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HMI software properties");
            }

            return properties;
        }
    }}