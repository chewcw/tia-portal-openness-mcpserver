using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public class UtilityTools
    {
        private readonly ILogger<UtilityTools> _logger;
        private readonly TiaPortalSessionManager _sessionManager;

        public UtilityTools(
            ILogger<UtilityTools> logger,
            TiaPortalSessionManager sessionManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
        }

        [McpServerTool, Description("Retrieve comprehensive project metadata including project name, file path, TIA Portal version, device count, and device list. Returns project information object. Prerequisites: Project must be open. Use this for project inventory, version tracking, documentation generation, or as initial context gathering before other operations. Essential for understanding project scope.")]
        public string utilities_get_project_info()
        {
            _logger.LogInformation("utilities_get_project_info called");

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

                var devices = project.Devices.Select(d => d.Name).ToList();

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        projectName = project.Name,
                        projectPath = project.Path?.FullName,
                        version = project.Version?.ToString(),
                        deviceCount = devices.Count,
                        devices = devices
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error getting project info");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error getting project info: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project info");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error getting project info: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Enumerate available TIA Portal libraries (.zal files) accessible to the current project for importing library objects. Returns library names and count. Prerequisites: Project must be open. Note: Library enumeration support varies by TIA Portal API version; may return empty list if API access unavailable. Use this to discover reusable library components before import operations.")]
        public string utilities_list_libraries()
        {
            _logger.LogInformation("utilities_list_libraries called");

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

                var libraries = new List<string>();

                var librariesProperty = project.GetType().GetProperty("Libraries");
                if (librariesProperty != null)
                {
                    var librariesValue = librariesProperty.GetValue(project);
                    if (librariesValue is IEnumerable enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            var nameProperty = item?.GetType().GetProperty("Name");
                            var nameValue = nameProperty?.GetValue(item)?.ToString();
                            if (!string.IsNullOrWhiteSpace(nameValue))
                            {
                                libraries.Add(nameValue!);
                            }
                        }
                    }
                }

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        libraryCount = libraries.Count,
                        libraries = libraries,
                        message = libraries.Count == 0
                            ? "No libraries found or library listing not available via this API"
                            : "Libraries listed successfully"
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error listing libraries");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error listing libraries: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing libraries");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error listing libraries: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }
    }
}
