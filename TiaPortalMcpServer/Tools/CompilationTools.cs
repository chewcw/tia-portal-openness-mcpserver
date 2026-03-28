using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
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

        [McpServerTool, Description("Compile the entire TIA Portal project including all devices, PLCs, and HMI targets. Returns compilation state (success/error/warning). Prerequisites: Project must be open. Use this to validate the entire project and identify compilation errors across all project components. Note: Compilation can be time-consuming for large projects.")]
        public string compilation_project()
        {
            _logger.LogInformation("compilation_project called");

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

                _logger.LogInformation("Compiling project '{ProjectName}'", project.Name);
                var result = TryCompileProject(project, out var compileState, out var compileMessage);
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

        [McpServerTool, Description("Compile PLC software for a specific device. Returns compilation state and identifies device-specific errors. Prerequisites: Project must be open, device must have PLC software. Use this for targeted compilation when working on a single device's logic. Faster than full project compilation for iterative development.")]
        public string compilation_software([Description("Device name")] string deviceName)
        {
            _logger.LogInformation("compilation_software called with deviceName='{DeviceName}'", deviceName);

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
                var result = TryCompileSoftware(software, out var compileState, out var compileMessage);
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

        private static bool TryCompileProject(Project project, out string? state, out string? message)
        {
            state = null;
            message = null;

            if (project == null)
            {
                message = "Project is null";
                return false;
            }

            var compileService = project.GetService<ICompilable>();
            if (compileService == null)
            {
                message = "Compile service not available for the project";
                return false;
            }

            CompilerResult result = compileService.Compile();
            if (result != null)
            {
                state = result.State.ToString();
            }

            return true;
        }

        private static bool TryCompileSoftware(PlcSoftware software, out string? state, out string? message)
        {
            state = null;
            message = null;

            if (software == null)
            {
                message = "Software is null";
                return false;
            }

            var compileService = software.GetService<ICompilable>();
            if (compileService == null)
            {
                message = "Compile service not available for the software";
                return false;
            }

            CompilerResult result = compileService.Compile();
            if (result != null)
            {
                state = result.State.ToString();
            }

            return true;
        }
    }
}
