using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Siemens.Engineering;

namespace TiaPortalMcpServer.Services
{
    /// <summary>
    /// Manages the current TIA Portal project session state
    /// </summary>
    public class TiaPortalSessionManager
    {
        private readonly TiaPortalService _portalService;
        private readonly ILogger<TiaPortalSessionManager> _logger;
        private readonly object _lock = new object();

        private Project? _currentProject;
        private string? _currentProjectPath;

        public TiaPortalSessionManager(
            TiaPortalService portalService,
            ILogger<TiaPortalSessionManager> logger)
        {
            _portalService = portalService;

            _portalService.GetOrCreatePortalInstance();
            var currentProject = _portalService.GetCurrentProject();
            if (currentProject != null)
            {
                _currentProject = currentProject;
                _currentProjectPath = currentProject.Path.ToString();
            }

            _logger = logger;
        }

        /// <summary>
        /// Gets the currently open project, or null if no project is open
        /// </summary>
        public Project? CurrentProject
        {
            get
            {
                lock (_lock)
                {
                    return _currentProject;
                }
            }
        }

        /// <summary>
        /// Gets the file path of the currently open project
        /// </summary>
        public string? CurrentProjectPath
        {
            get
            {
                lock (_lock)
                {
                    return _currentProjectPath;
                }
            }
        }

        /// <summary>
        /// Opens a project from the specified path
        /// </summary>
        public Project OpenProject(string projectPath)
        {
            lock (_lock)
            {
                if (_currentProject != null)
                {
                    throw new InvalidOperationException(
                        $"A project is already open: {_currentProjectPath}. Close it first."
                    );
                }

                if (!File.Exists(projectPath))
                {
                    throw new FileNotFoundException($"Project file not found: {projectPath}");
                }

                _logger.LogInformation("Opening project session: {ProjectPath}", projectPath);

                var project = _portalService.OpenProject(projectPath);
                _currentProject = project;
                _currentProjectPath = projectPath;

                _logger.LogInformation(
                    "Project session opened: {ProjectName} at {ProjectPath}",
                    project.Name,
                    projectPath
                );

                return project;
            }
        }

        /// <summary>
        /// Saves the currently open project
        /// </summary>
        public void SaveCurrentProject()
        {
            lock (_lock)
            {
                if (_currentProject == null)
                {
                    throw new InvalidOperationException("No project is currently open");
                }

                _logger.LogInformation("Saving current project: {ProjectName}", _currentProject.Name);
                _currentProject.Save();
                _logger.LogInformation("Project saved successfully");
            }
        }

        /// <summary>
        /// Closes the currently open project
        /// </summary>
        public void CloseCurrentProject()
        {
            lock (_lock)
            {
                if (_currentProject == null)
                {
                    throw new InvalidOperationException("No project is currently open");
                }

                _logger.LogInformation(
                    "Closing project session: {ProjectName}",
                    _currentProject.Name
                );

                var projectName = _currentProject.Name;
                _currentProject.Close();
                _currentProject = null;
                _currentProjectPath = null;

                _logger.LogInformation("Project session closed: {ProjectName}", projectName);
            }
        }

        /// <summary>
        /// Checks if a project is currently open
        /// </summary>
        public bool HasOpenProject()
        {
            lock (_lock)
            {
                return _currentProject != null;
            }
        }

        /// <summary>
        /// Gets information about the current session state
        /// </summary>
        public (bool hasProject, string? projectName, string? projectPath) GetSessionInfo()
        {
            lock (_lock)
            {
                return (
                    _currentProject != null,
                    _currentProject?.Name,
                    _currentProjectPath
                );
            }
        }
    }
}
