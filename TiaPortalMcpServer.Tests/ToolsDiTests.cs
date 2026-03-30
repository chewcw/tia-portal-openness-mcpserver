using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using TiaPortalMcpServer.Services;
using TiaPortalMcpServer.Extensions;
using Serilog;
using Serilog.Events;

namespace TiaPortalMcpServer.Tests
{
    [Trait("Category", "Unit")]
    public class ToolsDiTests
    {
        [Fact]
        public void Tool_types_are_resolvable_from_service_provider()
        {
            // Configure Serilog to write to stderr
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .WriteTo.Console(
                    standardErrorFromLevel: LogEventLevel.Verbose,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            var builder = Host.CreateApplicationBuilder(new string[] { });

            // register the same core services used by the app
            builder.Services.AddSingleton<TiaPortalService>();
            builder.Services.AddSingleton<TiaPortalSessionManager>();

            // Use Serilog for logging
            builder.Services.AddSerilog();

            // register only test tools that don't require TIA Portal
            builder.Services.AddSingleton<TiaPortalMcpServer.TestTools>();

            var sp = builder.Services.BuildServiceProvider();

            try
            {
                // tools must be resolvable from DI and usable
                var testTools = sp.GetService<TiaPortalMcpServer.TestTools>();
                Assert.NotNull(testTools);
                var greeting = testTools.hello_world();
                Assert.Equal("Hello from TIA MCP Server!", greeting);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
