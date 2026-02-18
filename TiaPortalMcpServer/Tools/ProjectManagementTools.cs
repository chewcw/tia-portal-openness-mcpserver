using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Siemens.Engineering;
using TiaPortalMcpServer.Models;
using TiaPortalMcpServer.Services;

namespace TiaPortalMcpServer
{
    [McpServerToolType]
    public class ProjectManagementTools
    {
        private readonly ILogger<ProjectManagementTools> _logger;
        private readonly TiaPortalService _portalService;
        private readonly TiaPortalSessionManager _sessionManager;

        public ProjectManagementTools(
            ILogger<ProjectManagementTools> logger,
            TiaPortalService portalService,
            TiaPortalSessionManager sessionManager)
        {
            _logger = logger;
            _portalService = portalService;
            _sessionManager = sessionManager;
        }

        [McpServerTool, Description("Create a new TIA project")]
        public string create_project(
            [Description("Name of the project")] string name,
            [Description("Path to save the project")] string path)
        {
            _logger.LogInformation("create_project called with name='{Name}', path='{Path}'", name, path);

            try
            {
                // Validate the path exists
                if (!Directory.Exists(path))
                {
                    _logger.LogError("Directory does not exist: '{Path}'", path);
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.InvalidParameter,
                            $"Directory does not exist: '{path}'"
                        )
                    );
                }

                // Check if a project is already open
                if (_sessionManager.HasOpenProject())
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.AlreadyOpen,
                            $"A project is already open: {_sessionManager.CurrentProjectPath}. Close it first."
                        )
                    );
                }

                _logger.LogDebug("Creating project '{Name}' at path '{Path}'...", name, path);

                var portal = _portalService.GetOrCreatePortalInstance();
                var project = portal.Projects.Create(new DirectoryInfo(path), name);

                _logger.LogDebug("Saving project...");
                project.Save();

                var projectPath = project.Path.FullName;

                // Close the newly created project so it can be opened via open_project
                _logger.LogDebug("Closing newly created project...");
                project.Close();

                _logger.LogInformation("Project '{ProjectName}' created successfully at '{ProjectPath}'", name, projectPath);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        projectName = name,
                        projectPath = projectPath,
                        message = $"Project '{name}' created successfully at '{projectPath}'. Use open_project to open it."
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error creating project '{Name}' at path '{Path}'", name, path);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error creating project: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project '{Name}' at path '{Path}'", name, path);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error creating project: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Open an existing TIA project")]
        public string open_project([Description("Path to the project file (.ap18, .ap17, etc.)")] string projectPath)
        {
            _logger.LogInformation("open_project called with projectPath='{ProjectPath}'", projectPath);

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

                var project = _sessionManager.OpenProject(projectPath);

                _logger.LogInformation("Project opened successfully: {ProjectName}", project.Name);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        projectName = project.Name,
                        projectPath = projectPath,
                        version = project.Version?.ToString(),
                        message = $"Project '{project.Name}' opened successfully"
                    })
                );
            }
            catch (InvalidOperationException opEx) when (opEx.Message.Contains("already open"))
            {
                _logger.LogWarning("Attempted to open project while one is already open");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.AlreadyOpen,
                        opEx.Message
                    )
                );
            }
            catch (FileNotFoundException fnfEx)
            {
                _logger.LogError(fnfEx, "Project file not found: {ProjectPath}", projectPath);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ProjectNotFound,
                        fnfEx.Message
                    )
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error opening project: {ProjectPath}", projectPath);
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
                _logger.LogError(ex, "Error opening project: {ProjectPath}", projectPath);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error opening project: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Open an existing TIA project with upgrade support")]
        public string open_project_with_upgrade(
            [Description("Path to the project file (.ap18, .ap17, etc.)")] string projectPath)
        {
            _logger.LogInformation("open_project_with_upgrade called with projectPath='{ProjectPath}'", projectPath);

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

                var project = _sessionManager.OpenProjectWithUpgrade(projectPath);

                _logger.LogInformation("Project opened with upgrade successfully: {ProjectName}", project.Name);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        projectName = project.Name,
                        projectPath = projectPath,
                        version = project.Version?.ToString(),
                        message = $"Project '{project.Name}' opened with upgrade successfully"
                    })
                );
            }
            catch (InvalidOperationException opEx) when (opEx.Message.Contains("already open"))
            {
                _logger.LogWarning("Attempted to open project while one is already open");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.AlreadyOpen,
                        opEx.Message
                    )
                );
            }
            catch (FileNotFoundException fnfEx)
            {
                _logger.LogError(fnfEx, "Project file not found: {ProjectPath}", projectPath);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ProjectNotFound,
                        fnfEx.Message
                    )
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error opening project with upgrade: {ProjectPath}", projectPath);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error opening project with upgrade: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening project with upgrade: {ProjectPath}", projectPath);
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error opening project with upgrade: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Save the current project")]
        public string save_project()
        {
            _logger.LogInformation("save_project called");

            try
            {
                if (!_sessionManager.HasOpenProject())
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.NoProject,
                            "No project is currently open. Use open_project first."
                        )
                    );
                }

                _sessionManager.SaveCurrentProject();

                var projectName = _sessionManager.CurrentProject?.Name;
                _logger.LogInformation("Project saved successfully: {ProjectName}", projectName);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        projectName = projectName,
                        message = $"Project '{projectName}' saved successfully"
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error saving project");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error saving project: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving project");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error saving project: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Close the current TIA project")]
        public string close_project()
        {
            _logger.LogInformation("close_project called");

            try
            {
                if (!_sessionManager.HasOpenProject())
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.NoProject,
                            "No project is currently open"
                        )
                    );
                }

                var projectName = _sessionManager.CurrentProject?.Name;
                _sessionManager.CloseCurrentProject();

                _logger.LogInformation("Project closed successfully: {ProjectName}", projectName);

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        projectName = projectName,
                        message = $"Project '{projectName}' closed successfully"
                    })
                );
            }
            catch (COMException comEx)
            {
                _logger.LogError(comEx, "COM error closing project");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.ComError,
                        $"COM error closing project: {comEx.Message}",
                        comEx.ToString()
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing project");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error closing project: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }

        [McpServerTool, Description("Get information about the current session")]
        public string get_session_info()
        {
            _logger.LogInformation("get_session_info called");

            try
            {
                var (hasProject, projectName, projectPath) = _sessionManager.GetSessionInfo();

                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateSuccess(new
                    {
                        hasOpenProject = hasProject,
                        projectName = projectName,
                        projectPath = projectPath,
                        portalActive = _portalService.IsPortalActive
                    })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session info");
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.TiaError,
                        $"Error getting session info: {ex.Message}",
                        ex.ToString()
                    )
                );
            }
        }
    }
}
