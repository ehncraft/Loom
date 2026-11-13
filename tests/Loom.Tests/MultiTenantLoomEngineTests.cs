using Loom.Core;
using Loom.Schema;
using Loom.Evaluator;
using Loom.Relationships;

namespace Loom.Tests;

public class MultiTenantLoomEngineTests
{
    private static readonly LoomSchema Schema = SchemaParser.Parse(
        """
        definition user {}

        definition document {
            relation owner: user
            permission edit = owner
        }
        """);

    [Fact]
    public async Task CheckAsync_DifferentTenants_AreIsolated()
    {
        var tenantTuples = new Dictionary<string, InMemoryRelationTupleStore>(StringComparer.Ordinal)
        {
            ["acme"] = new InMemoryRelationTupleStore([
                new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice")),
            ]),
            ["globex"] = new InMemoryRelationTupleStore([]),
        };

        var engine = new MultiTenantLoomEngine(
            (tenantId, _) => Task.FromResult(new LoomEvaluator(Schema, tenantTuples[tenantId])));

        var acmeResult = await engine.CheckAsync(
            "acme", new ObjectRef("document", "1"), "edit", new SubjectRef("user", "alice"));
        var globexResult = await engine.CheckAsync(
            "globex", new ObjectRef("document", "1"), "edit", new SubjectRef("user", "alice"));

        Assert.IsType<CheckResult.Permit>(acmeResult);
        Assert.IsType<CheckResult.Deny>(globexResult);
    }

    [Fact]
    public async Task GetEngineAsync_SecondCall_ReusesCachedEngineWithoutInvokingFactoryAgain()
    {
        var factoryCalls = 0;
        var engine = new MultiTenantLoomEngine((_, _) =>
        {
            factoryCalls++;
            return Task.FromResult(new LoomEvaluator(Schema, new InMemoryRelationTupleStore()));
        });

        await engine.GetEngineAsync("acme", CancellationToken.None);
        await engine.GetEngineAsync("acme", CancellationToken.None);

        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task InvalidateAsync_EvictsCachedEngine_NextCallRebuildsFromFactory()
    {
        var factoryCalls = 0;
        var engine = new MultiTenantLoomEngine((_, _) =>
        {
            factoryCalls++;
            return Task.FromResult(new LoomEvaluator(Schema, new InMemoryRelationTupleStore()));
        });

        await engine.GetEngineAsync("acme", CancellationToken.None);
        await engine.InvalidateAsync("acme");
        await engine.GetEngineAsync("acme", CancellationToken.None);

        Assert.Equal(2, factoryCalls);
    }
}
