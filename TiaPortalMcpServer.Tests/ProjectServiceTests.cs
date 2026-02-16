using Xunit;
using TiaPortalMcpServer.ProjectManagement;

namespace TiaPortalMcpServer.Tests
{
    public class ProjectServiceTests
    {
        [Fact]
        public void ProjectService_CanBeConstructed()
        {
            var logger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<ProjectService>>().Object;
            var service = new ProjectService(logger);
            Assert.NotNull(service);
        }
    }
}
