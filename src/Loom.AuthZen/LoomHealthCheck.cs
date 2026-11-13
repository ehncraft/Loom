using Loom.Evaluator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Loom.AuthZen;

internal sealed class LoomHealthCheck(IServiceProvider services) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            services.GetRequiredService<LoomEvaluator>();
            return Task.FromResult(HealthCheckResult.Healthy("LoomEvaluator is available."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("LoomEvaluator failed to resolve.", ex));
        }
    }
}
