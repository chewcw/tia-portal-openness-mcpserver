using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using TiaPortalMcpServer.Services;
using TiaPortalMcpServer.Extensions;
using Siemens.Collaboration.Net;

namespace TiaPortalMcpServer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                RegisterSiemensAssembly();

                if (Api.Global.Openness().IsUserInGroup())
                {
                    await RunStdioHost(args);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error in Main: {ex}");
                throw;
            }
        }

        static void RegisterSiemensAssembly()
        {
            var tiaMajorVersion = 20;
            Api.Global.Openness().Initialize(tiaMajorVersion: tiaMajorVersion);
        }

        static async Task RunStdioHost(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.AddConsole(consoleLogOptions =>
            {
                // Configure all logs to go to stderr to avoid corrupting MCP protocol on stdout
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
            });
            builder.Logging.SetMinimumLevel(LogLevel.Trace);

            // Register TIA Portal services
            builder.Services.AddSingleton<TiaPortalService>();
            builder.Services.AddSingleton<TiaPortalSessionManager>();

            // Register MCP tool classes into DI so their constructors receive injected services
            builder.Services.AddMcpToolTypesFromAssembly();

            // Configure MCP server and register discovered tools/prompts
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly()
                .WithPromptsFromAssembly();

            var host = builder.Build();

            await host.RunAsync();
        }
    }
}
