using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace TiaPortalMcpServer.Extensions
{
    /// <summary>
    /// Helper extensions to register MCP tool classes with the application's
    /// dependency injection container so they receive constructor injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Scans the provided assembly for types decorated with <see cref="McpServerToolTypeAttribute"/>
        /// and registers each concrete class type into DI as a singleton so tools can receive
        /// services from the application's IServiceProvider via constructor injection.
        /// </summary>
        public static IServiceCollection AddMcpToolTypesFromAssembly(this IServiceCollection services, Assembly? assembly = null)
        {
            assembly ??= Assembly.GetExecutingAssembly();

            var toolTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttributes(typeof(McpServerToolTypeAttribute), inherit: false).Any())
                .ToArray();

            foreach (var tt in toolTypes)
            {
                // register concrete tool type so constructor injection works
                services.AddSingleton(tt);
            }

            return services;
        }
    }
}
