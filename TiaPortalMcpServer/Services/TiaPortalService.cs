using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Siemens.Engineering;

namespace TiaPortalMcpServer.Services
{
    /// <summary>
    /// Manages the lifecycle of a singleton TIA Portal instance
    /// </summary>
    public class TiaPortalService
    {
        private readonly ILogger<TiaPortalService> _logger;
        private TiaPortal? _tiaPortal;
        private readonly object _lock = new object();
        private bool _disposed = false;

        public TiaPortalService(ILogger<TiaPortalService> logger)
        {
            _logger = logger;
        }

        public bool IsPortalActive => _tiaPortal != null;

        public TiaPortal? CurrentPortal => _tiaPortal;

        public TiaPortal GetOrCreatePortalInstance()
        {
            lock (_lock)
            {
                if (_tiaPortal == null)
                {
                    _logger.LogInformation("Creating new TIA Portal instance...");
                    try
                    {
                        var processes = TiaPortal.GetProcesses();
                        if (processes.Any())
                        {
                            _tiaPortal = processes.First().Attach();
                        }
                        else
                        {
                            _tiaPortal = new TiaPortal(TiaPortalMode.WithoutUserInterface);
                        }
                    }
                    catch (COMException comEx)
                    {
                        _logger.LogError(comEx, "COM error creating TIA Portal instance");
                        throw new InvalidOperationException(
                            $"Failed to create TIA Portal instance: {comEx.Message}",
                            comEx
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating TIA Portal instance");
                        throw;
                    }
                }

                return _tiaPortal;
            }
        }

        public Project OpenProject(string projectPath)
        {
            lock (_lock)
            {
                var portal = GetOrCreatePortalInstance();

                if (!File.Exists(projectPath))
                {
                    throw new FileNotFoundException($"Project file not found: {projectPath}");
                }

                _logger.LogInformation("Opening project: {ProjectPath}", projectPath);

                try
                {
                    var fileInfo = new FileInfo(projectPath);
                    var project = portal.Projects.Open(fileInfo);
                    _logger.LogInformation("Project opened successfully: {ProjectName}", project.Name);
                    return project;
                }
                catch (COMException comEx)
                {
                    _logger.LogError(comEx, "COM error opening project: {ProjectPath}", projectPath);
                    throw new InvalidOperationException(
                        $"Failed to open project: {comEx.Message}",
                        comEx
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error opening project: {ProjectPath}", projectPath);
                    throw;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                if (_tiaPortal != null)
                {
                    _logger.LogInformation("Disposing TIA Portal instance...");
                    try
                    {
                        _tiaPortal.Dispose();
                        _logger.LogInformation("TIA Portal instance disposed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing TIA Portal instance");
                    }
                    finally
                    {
                        _tiaPortal = null;
                    }
                }

                _disposed = true;
            }
        }
    }
}
