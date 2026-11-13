using Loom.Caveats;
using Loom.Core;
using Loom.Relationships;

namespace Loom.AspNetCore;

public sealed class LoomOptions
{
    internal string? SchemaText { get; private set; }

    internal IRelationTupleReader? Tuples { get; private set; }

    internal IReadOnlyDictionary<string, CaveatDefinition>? Caveats { get; private set; }

    internal Action<LoomClaimsMappingOptions>? ConfigureClaimsMappingAction { get; private set; }

    public void AddSchema(string schemaText) => SchemaText = schemaText;

    public void AddTupleStore(IRelationTupleReader tuples) => Tuples = tuples;

    public void AddCaveats(IReadOnlyDictionary<string, CaveatDefinition> caveats) => Caveats = caveats;

    public void ConfigureClaimsMapping(Action<LoomClaimsMappingOptions> configure) =>
        ConfigureClaimsMappingAction = configure;
}
