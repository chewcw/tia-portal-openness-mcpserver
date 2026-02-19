using System;
using System.Linq;
using Siemens.Engineering.HW;
using Siemens.Engineering.SW;

namespace TiaPortalMcpServer.Services
{
    internal static class TiaPortalSoftwareHelper
    {
        public static (PlcSoftware? software, string? errorMessage) TryGetPlcSoftwareWithDiagnostics(Device device)
        {
            if (device == null)
            {
                return (null, "Device is null");
            }

            if (device.DeviceItems == null)
            {
                return (null, $"Device '{device.Name}' has no DeviceItems collection");
            }

            if (device.DeviceItems.Count == 0)
            {
                return (null, $"Device '{device.Name}' has no DeviceItems. This device may not be a PLC or may not have software configured yet.");
            }

            var softwareContainerType = device.GetType().Assembly.GetType("Siemens.Engineering.SW.SoftwareContainer");
            if (softwareContainerType == null)
            {
                return (null, "SoftwareContainer type not found in TIA Portal API. Check TIA Portal Openness installation.");
            }

            var checkedItems = 0;
            foreach (var deviceItem in device.DeviceItems)
            {
                checkedItems++;
                var getServiceMethod = deviceItem.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "GetService" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

                if (getServiceMethod == null)
                {
                    continue;
                }

                try
                {
                    var generic = getServiceMethod.MakeGenericMethod(softwareContainerType);
                    var service = generic.Invoke(deviceItem, null);
                    if (service == null)
                    {
                        continue;
                    }

                    var softwareProperty = softwareContainerType.GetProperty("Software");
                    var softwareValue = softwareProperty?.GetValue(service);
                    if (softwareValue is PlcSoftware plcSoftware)
                    {
                        return (plcSoftware, null);
                    }
                }
                catch (Exception)
                {
                    // Continue checking other device items
                    continue;
                }
            }

            return (null, $"Device '{device.Name}' has {device.DeviceItems.Count} DeviceItem(s), but none contain PLC software. " +
                         $"This may be an HMI, network device, or unconfigured PLC. Use 'list_devices' to see device types.");
        }
    }
}
