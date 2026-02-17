using System;
using System.Linq;
using Siemens.Engineering.HW;
using Siemens.Engineering.SW;

namespace TiaPortalMcpServer.Services
{
    internal static class TiaPortalSoftwareHelper
    {
        public static PlcSoftware? TryGetPlcSoftware(Device device)
        {
            if (device == null)
            {
                return null;
            }

            var softwareContainerType = device.GetType().Assembly.GetType("Siemens.Engineering.SW.SoftwareContainer");
            if (softwareContainerType == null)
            {
                return null;
            }

            foreach (var deviceItem in device.DeviceItems)
            {
                var getServiceMethod = deviceItem.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "GetService" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

                if (getServiceMethod == null)
                {
                    continue;
                }

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
                    return plcSoftware;
                }
            }

            return null;
        }
    }
}
