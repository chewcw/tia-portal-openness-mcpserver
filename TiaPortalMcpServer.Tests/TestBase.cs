using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Siemens.Collaboration.Net;
using TiaPortalMcpServer.Services;
using TiaPortalMcpServer.Extensions;
using Xunit;
using Serilog;
using Serilog.Events;

namespace TiaPortalMcpServer.Tests
{
    public abstract class TestBase : IDisposable
    {
        protected IServiceProvider ServiceProvider { get; private set; }
        protected Microsoft.Extensions.Logging.ILogger Logger { get; private set; }

        protected TestBase()
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

            // Initialize Siemens Openness API
            try
            {
                var tiaMajorVersion = 20;
                Api.Global.Openness().Initialize(tiaMajorVersion: tiaMajorVersion);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TIA Portal Openness API initialization failed");
                throw;
            }

            // Check if user is in the required group
            if (!Api.Global.Openness().IsUserInGroup())
            {
                Log.Error("User is not in the required TIA Portal user group");
                throw new Exception("User is not in the required TIA Portal user group. Ensure proper permissions.");
            }

            // Set up DI container similar to the main application
            var builder = Host.CreateApplicationBuilder(new string[] { });

            builder.Services.AddSingleton<TiaPortalService>();
            builder.Services.AddSingleton<TiaPortalSessionManager>();
            builder.Services.AddSingleton<BlocksAdapter>();
            builder.Services.AddSingleton<HmiTargetAdapter>();

            // Register tool types
            builder.Services.AddMcpToolTypesFromAssembly();

            // Use Serilog for logging
            builder.Services.AddSerilog();

            ServiceProvider = builder.Services.BuildServiceProvider();

            Logger = ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TestBase>>();
        }

        public void Dispose()
        {
            // Cleanup if needed
            (ServiceProvider as IDisposable)?.Dispose();
            Log.CloseAndFlush();
        }
    }
}