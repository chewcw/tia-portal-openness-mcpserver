using System;
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

        [McpServerTool, Description("Create a new TIA Portal project in the specified directory. Returns the project path. Prerequisites: Directory must exist, no project currently open. Note: Newly created project is automatically closed after creation; use projects_open to open it for editing.")]
        public string projects_create(
            [Description("Name of the project")] string name,
            [Description("Path to save the project")] string path)
        {
_logger.LogInformation("projects_create called with name='{Name}', path='{Path}'", name, path);

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

        [McpServerTool, Description("Open an existing TIA Portal project file for editing. Returns project metadata including name, path, and version. Prerequisites: Project file must exist, no other project currently open. Use this before any device, block, or tag operations. Supports .ap18, .ap17, .ap16 formats.")]
        public string projects_open([Description("Path to the project file (.ap18, .ap17, etc.)")] string projectPath)
        {
_logger.LogInformation("projects_open called with projectPath='{ProjectPath}'", projectPath);

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

        [McpServerTool, Description("Interactively create a new TIA Portal project. If name/path are missing, the server will ask the user via MCP Apps/elicitation.")]
        public async Task<string> projects_create_interactive(
            McpServer server,
            [Description("Optional project name")] string? name,
            [Description("Optional path to save the project")] string? path,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
            {
                if (server.ClientCapabilities?.Elicitation == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.OperationNotSupported,
                            "Client does not support elicitation. Provide name/path or use a client with MCP Apps/elicitation support."
                        )
                    );
                }

                var schema = new ElicitRequestParams.RequestSchema();
                if (string.IsNullOrWhiteSpace(name))
                {
                    schema.Properties["name"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Project name"
                    };
                }
                if (string.IsNullOrWhiteSpace(path))
                {
                    schema.Properties["path"] = new ElicitRequestParams.StringSchema
                    {
                        Description = "Directory where the project should be created"
                    };
                }

                schema.Required = schema.Properties.Keys.ToArray();

                var response = await server.ElicitAsync(new ElicitRequestParams
                {
                    Message = "Missing required project details. Please provide the requested fields.",
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

                if (string.IsNullOrWhiteSpace(name) &&
                    response.Content != null &&
                    response.Content.TryGetValue("name", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String)
                {
                    name = nameElement.GetString();
                }

                if (string.IsNullOrWhiteSpace(path) &&
                    response.Content != null &&
                    response.Content.TryGetValue("path", out var pathElement) &&
                    pathElement.ValueKind == JsonValueKind.String)
                {
                    path = pathElement.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
            {
                return JsonConvert.SerializeObject(
                    ToolResponse<object>.CreateError(
                        ErrorCodes.InvalidParameter,
                        "name and path are required."
                    )
                );
            }

            return projects_create(name, path);
        }

        [McpServerTool, Description("Interactively open an existing TIA Portal project. If projectPath is missing, the server will ask the user for it via MCP Apps/elicitation.")]
        public async Task<string> projects_open_interactive(
            McpServer server,
            [Description("Optional path to the project file (.ap18, .ap17, etc.)")] string? projectPath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                if (server.ClientCapabilities?.Elicitation == null)
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.OperationNotSupported,
                            "Client does not support elicitation. Provide projectPath or use a client with MCP Apps/elicitation support."
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
                    Message = "Project path is required to open a TIA Portal project. Please provide the full file path.",
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
            }

            return projects_open(projectPath);
        }

        [McpServerTool, Description("Open an existing TIA Portal project file and automatically upgrade it to the current TIA Portal version if needed. Use this when opening projects created in older TIA Portal versions. Returns upgraded project metadata. Prerequisites: Project file must exist, no other project currently open. Warning: Upgrade is permanent; backup project first.")]
        public string projects_open_with_upgrade(
            [Description("Path to the project file (.ap18, .ap17, etc.)")] string projectPath)
        {
            _logger.LogInformation("projects_open_with_upgrade called with projectPath='{ProjectPath}'", projectPath);

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

        [McpServerTool, Description("Save all changes to the currently open TIA Portal project. Returns confirmation with project name. Prerequisites: A project must be open. Best practice: Call this after making modifications (adding blocks, tags, devices) to persist changes before closing or compiling.")]
        public string projects_save()
        {
            _logger.LogInformation("projects_save called");

            try
            {
                if (!_sessionManager.HasOpenProject())
                {
                    return JsonConvert.SerializeObject(
                        ToolResponse<object>.CreateError(
                            ErrorCodes.NoProject,
                            "No project is currently open. Use projects_open first."
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

        [McpServerTool, Description("Close the currently open TIA Portal project and release resources. Returns confirmation with closed project name. Prerequisites: A project must be open. Note: Unsaved changes will be lost unless projects_save was called first. Recommended before opening a different project.")]
        public string projects_close()
        {
            _logger.LogInformation("projects_close called");

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

        [McpServerTool, Description("Retrieve current TIA Portal session information including whether a project is open, project name, path, and portal instance status. Returns session metadata. No prerequisites. Use this to check session state before performing operations that require an open project.")]
        public string projects_get_session_info()
        {
            _logger.LogInformation("projects_get_session_info called");

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
