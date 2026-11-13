using Microsoft.Extensions.DependencyInjection;

namespace Loom.AuthZen;

public static class HealthCheckServiceCollectionExtensions
{
    public static IServiceCollection AddLoomHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<LoomHealthCheck>("loom");
        return services;
    }
}
