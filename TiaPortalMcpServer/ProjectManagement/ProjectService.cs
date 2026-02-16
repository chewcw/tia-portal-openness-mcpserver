using Microsoft.Extensions.Logging;

namespace TiaPortalMcpServer.ProjectManagement
{
    public class ProjectService
    {
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(ILogger<ProjectService> logger)
        {
            _logger = logger;
        }
    }
}
