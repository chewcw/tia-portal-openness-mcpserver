using Microsoft.Extensions.DependencyInjection;

namespace TiaPortalMcpServer
{
    /// <summary>
    /// Organizational helper for tool registrations. Currently a no-op placeholder so
    /// callers can migrate to a centralized registration point in future.
    /// </summary>
    public static class ToolsModule
    {
        public static IServiceCollection AddTiaTools(this IServiceCollection services)
        {
            return services;
        }
    }
}
