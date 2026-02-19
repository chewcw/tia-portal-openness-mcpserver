using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public class CompilationTools
    {
        private readonly ILogger<CompilationTools> _logger;

        private readonly TiaPortalSessionManager _sessionManager;

        public CompilationTools(
            ILogger<CompilationTools> logger,
            TiaPortalSessionManager sessionManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
        }

        [McpServerTool, Description("Compile the entire project")]
        public string compile_project()
        {
            _logger.LogInformation("compile_project called");

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

                _logger.LogInformation("Compiling project '{ProjectName}'", project.Name);
                var result = TryCompile(project, out var compileState, out var compileMessage);
                if (!result)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.CompilationError,
                            compileMessage ?? "Compile service not available for the current project"
                        )
                    );
                }

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        projectName = project.Name,
                        resultState = compileState,
                        message = "Project compilation completed"
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error compiling project");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error compiling project: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compiling project");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.CompilationError,
                        $"Error compiling project: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Compile PLC software for a specific device")]
        public string compile_software([Description("Device name")] string deviceName)
        {
            _logger.LogInformation("compile_software called with deviceName='{DeviceName}'", deviceName);

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

                var software = _sessionManager.PortalService.GetPlcSoftware(device);
                if (software == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.TiaError,
                            $"Device '{deviceName}' does not have PLC software"
                        )
                    );
                }

                _logger.LogInformation("Compiling PLC software for device '{DeviceName}'", deviceName);
                var result = TryCompile(software, out var compileState, out var compileMessage);
                if (!result)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.CompilationError,
                            compileMessage ?? "Compile service not available for PLC software"
                        )
                    );
                }

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        deviceName = deviceName,
                        resultState = compileState,
                        message = "PLC software compilation completed"
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error compiling software for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error compiling software: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compiling software for device '{DeviceName}'", deviceName);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.CompilationError,
                        $"Error compiling software: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        private static bool TryCompile(object engineeringObject, out string? state, out string? message)
        {
            state = null;
            message = null;

            if (engineeringObject == null)
            {
                message = "Engineering object is null";
                return false;
            }

            var assembly = engineeringObject.GetType().Assembly;
            var compileServiceType = assembly.GetType("Siemens.Engineering.Compiler.CompileService");
            if (compileServiceType == null)
            {
                message = "Compile service type not found in Siemens.Engineering assembly";
                return false;
            }

            var getServiceMethod = engineeringObject.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "GetService" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

            if (getServiceMethod == null)
            {
                message = "GetService method not found on engineering object";
                return false;
            }

            var generic = getServiceMethod.MakeGenericMethod(compileServiceType);
            var service = generic.Invoke(engineeringObject, null);
            if (service == null)
            {
                message = "Compile service not available for the engineering object";
                return false;
            }

            var compileMethod = compileServiceType.GetMethod("Compile", BindingFlags.Public | BindingFlags.Instance);
            if (compileMethod == null)
            {
                message = "Compile method not found on compile service";
                return false;
            }

            var result = compileMethod.Invoke(service, null);
            if (result != null)
            {
                var stateProperty = result.GetType().GetProperty("State");
                state = stateProperty?.GetValue(result)?.ToString();
            }

            return true;
        }
    }
}
