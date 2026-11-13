using System.Net;

using Loom.AuthZen;
using Loom.Schema;
using Loom.Evaluator;
using Loom.Relationships;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Loom.Tests;

public class LoomHealthCheckTests
{
    [Fact]
    public async Task HealthEndpoint_EvaluatorResolves_Returns200()
    {
        var host = await BuildHostAsync(services =>
            services.AddSingleton(new LoomEvaluator(SchemaParser.Parse("definition user {}"), new InMemoryRelationTupleStore())));
        try
        {
            var response = await host.GetTestClient().GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Fact]
    public async Task HealthEndpoint_EvaluatorFailsToResolve_Returns503WithoutCrashingTheEndpoint()
    {
        var host = await BuildHostAsync(
            services => services.AddSingleton<LoomEvaluator>(_ => throw new InvalidOperationException("simulated bad schema")));
        try
        {
            var response = await host.GetTestClient().GetAsync("/health");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    private static async Task<IHost> BuildHostAsync(Action<IServiceCollection> registerEvaluator)
    {
        var builder = new HostBuilder().ConfigureWebHost(webHost =>
        {
            webHost.UseTestServer();
            webHost.ConfigureServices(services =>
            {
                services.AddRouting();
                registerEvaluator(services);
                services.AddLoomHealthChecks();
            });
            webHost.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapLoomHealthChecks());
            });
        });

        return await builder.StartAsync();
    }
}
