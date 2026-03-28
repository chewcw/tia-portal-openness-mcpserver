using System;
using System.IO;
using System.Linq;
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
            _logger = logger;

            _portalService.GetOrCreatePortalInstance();
            var currentProject = _portalService.GetCurrentProject();
            if (currentProject != null)
            {
                try
                {
                    _ = currentProject.Name;
                    _currentProject = currentProject;
                    _currentProjectPath = currentProject.Path.ToString();
                }
                catch (Exception)
                {
                    _currentProject = null;
                }
            }
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
        /// Gets the TIA Portal service
        /// </summary>
        public TiaPortalService PortalService => _portalService;

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
        /// Opens a project with upgrade from the specified path
        /// </summary>
        public Project OpenProjectWithUpgrade(string projectPath)
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

                _logger.LogInformation("Opening project session with upgrade: {ProjectPath}", projectPath);

                var project = _portalService.OpenProjectWithUpgrade(projectPath);
                _currentProject = project;
                _currentProjectPath = projectPath;

                _logger.LogInformation(
                    "Project session opened with upgrade: {ProjectName} at {ProjectPath}",
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
                    return;
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
        /// Archives the currently open project
        /// </summary>
        public void ArchiveCurrentProject(string targetDirectory, string targetName, Siemens.Engineering.ProjectArchivationMode mode = Siemens.Engineering.ProjectArchivationMode.None)
        {
            lock (_lock)
            {
                if (_currentProject == null)
                {
                    throw new InvalidOperationException("No project is currently open");
                }

                _logger.LogInformation("Archiving current project: {ProjectName}", _currentProject.Name);
                _portalService.ArchiveProject(_currentProject, targetDirectory, targetName, mode);
                _logger.LogInformation("Project archived successfully");
            }
        }

        /// <summary>
        /// Retrieves a project from archive and opens it
        /// </summary>
        public Project RetrieveAndOpenProject(string sourcePath, string targetDirectory, bool withUpgrade = false)
        {
            lock (_lock)
            {
                if (_currentProject != null)
                {
                    throw new InvalidOperationException(
                        $"A project is already open: {_currentProjectPath}. Close it first."
                    );
                }

                _logger.LogInformation("Retrieving and opening project from archive: {SourcePath}", sourcePath);

                var project = _portalService.RetrieveProject(sourcePath, targetDirectory, withUpgrade);
                _currentProject = project;
                _currentProjectPath = project.Path.FullName;

                _logger.LogInformation(
                    "Project retrieved and opened: {ProjectName} at {ProjectPath}",
                    project.Name,
                    _currentProjectPath
                );

                return project;
            }
        }
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

        /// <summary>
        /// Finds a device by path (currently supports device name only)
        /// </summary>
        public Siemens.Engineering.HW.Device? FindDevice(string devicePath)
        {
            lock (_lock)
            {
                if (_currentProject == null)
                {
                    return null;
                }

                // For now, assume devicePath is just the device name
                // Future: support hierarchical paths like "Group/Device"
                return _currentProject.Devices.FirstOrDefault(d => d.Name == devicePath);
            }
        }

        /// <summary>
        /// Finds a device item by path (device/item)
        /// </summary>
        public Siemens.Engineering.HW.DeviceItem? FindDeviceItem(string devicePath, string itemPath)
        {
            var device = FindDevice(devicePath);
            if (device == null)
            {
                return null;
            }

            // For now, assume itemPath is just the item name
            return device.DeviceItems.FirstOrDefault(di => di.Name == itemPath);
        }
    }
}
