using Loom.Caveats;
using Loom.Core;
using Loom.Schema;
using Loom.Evaluator;
using Loom.Relationships;
using Loom.Testing;

namespace Loom.Tests;

public class LoomEvaluatorAssertionsTests
{
    private static readonly LoomSchema Schema = SchemaParser.Parse(
        """
        definition user {}

        definition document {
            relation owner: user
            permission edit = owner
        }
        """);

    private static LoomEvaluator CreateEvaluator() => new(
        Schema,
        new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice")),
        ]));

    [Fact]
    public async Task ShouldPermitAsync_WhenPermitted_DoesNotThrow()
    {
        var evaluator = CreateEvaluator();

        await evaluator.ShouldPermitAsync(new ObjectRef("document", "1"), "edit", new SubjectRef("user", "alice"));
    }

    [Fact]
    public async Task ShouldPermitAsync_WhenDenied_Throws()
    {
        var evaluator = CreateEvaluator();

        await Assert.ThrowsAsync<LoomAssertionException>(() =>
            evaluator.ShouldPermitAsync(new ObjectRef("document", "1"), "edit", new SubjectRef("user", "bob")));
    }

    [Fact]
    public async Task ShouldDenyAsync_WhenDenied_DoesNotThrow()
    {
        var evaluator = CreateEvaluator();

        await evaluator.ShouldDenyAsync(new ObjectRef("document", "1"), "edit", new SubjectRef("user", "bob"));
    }

    [Fact]
    public async Task ShouldDenyAsync_WhenPermitted_Throws()
    {
        var evaluator = CreateEvaluator();

        await Assert.ThrowsAsync<LoomAssertionException>(() =>
            evaluator.ShouldDenyAsync(new ObjectRef("document", "1"), "edit", new SubjectRef("user", "alice")));
    }

    [Fact]
    public async Task ShouldRequireCaveatContextAsync_WhenCaveated_DoesNotThrow()
    {
        var caveats = CaveatParser.Parse(
            """
            caveat match_region(expected_regions list<string>, observed_region string) {
              observed_region in expected_regions
            }
            """);

        var tuple = new RelationTuple(
            new ObjectRef("document", "1"),
            "viewer",
            new SubjectRef("user", "alice"),
            CaveatName: "match_region",
            CaveatContext: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["expected_regions"] = new List<string> { "eu-west-1" },
            });

        var schema = SchemaParser.Parse(
            """
            definition user {}

            definition document {
                relation viewer: user
                permission view = viewer
            }
            """);

        var evaluator = new LoomEvaluator(schema, new InMemoryRelationTupleStore([tuple]), caveats);

        await evaluator.ShouldRequireCaveatContextAsync(
            new ObjectRef("document", "1"), "view", new SubjectRef("user", "alice"), "observed_region");
    }
}
