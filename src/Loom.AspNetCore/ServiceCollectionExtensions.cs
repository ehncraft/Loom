using Loom.Core;
using Loom.Evaluator;
using Loom.Relationships;
using Loom.Schema;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.AspNetCore;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLoom(this IServiceCollection services, Action<LoomOptions> configure)
    {
        var options = new LoomOptions();
        configure(options);

        if (options.SchemaText is null)
        {
            throw new InvalidOperationException("AddLoom requires a schema -- call options.AddSchema(schemaText).");
        }

        var claimsMappingOptions = new LoomClaimsMappingOptions();
        options.ConfigureClaimsMappingAction?.Invoke(claimsMappingOptions);
        services.AddSingleton(claimsMappingOptions);
        services.AddSingleton<IClaimsPrincipalMapper>(new ClaimsPrincipalMapper(claimsMappingOptions));

        services.AddSingleton(sp =>
        {
            var schema = SchemaParser.Parse(options.SchemaText!);
            var tuples = options.Tuples ?? new InMemoryRelationTupleStore();
            return new LoomEvaluator(schema, tuples, options.Caveats);
        });

        services.AddSingleton<IAuthorizationHandler, LoomAuthorizationHandler>();

        return services;
    }
}
