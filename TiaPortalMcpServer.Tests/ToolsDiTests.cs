using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using TiaPortalMcpServer.Services;
using TiaPortalMcpServer.Extensions;

namespace TiaPortalMcpServer.Tests
{
    public class ToolsDiTests
    {
        [Fact]
        public void Tool_types_are_resolvable_from_service_provider()
        {
            var builder = Host.CreateApplicationBuilder(new string[] { });

            // register the same core services used by the app
            builder.Services.AddSingleton<TiaPortalService>();
            builder.Services.AddSingleton<TiaPortalSessionManager>();

            // register tool types (mirrors Program.cs behavior)
            builder.Services.AddMcpToolTypesFromAssembly(typeof(TiaPortalMcpServer.TestTools).Assembly);

            var sp = builder.Services.BuildServiceProvider();

            // tools must be resolvable from DI and usable
            var testTools = sp.GetService<TiaPortalMcpServer.TestTools>();
            Assert.NotNull(testTools);
            var greeting = testTools.hello_world();
            Assert.Equal("Hello from TIA MCP Server!", greeting);

            var pmTools = sp.GetService<TiaPortalMcpServer.ProjectManagementTools>();
            Assert.NotNull(pmTools);
        }
    }
}
