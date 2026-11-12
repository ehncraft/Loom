using Loom.Caveats;
using Loom.Core;
using Loom.Schema;
using Loom.Evaluator;
using Loom.Relationships;

namespace Loom.Tests;

public class CaveatedCheckTests
{
    private static readonly IReadOnlyDictionary<string, CaveatDefinition> MatchRegionCaveat = CaveatParser.Parse(
        """
        caveat match_region(expected_regions list<string>, observed_region string) {
          observed_region in expected_regions
        }
        """);

    private static readonly LoomSchema ViewerSchema = SchemaParser.Parse(
        """
        definition user {}

        definition document {
            relation viewer: user
            permission view = viewer
        }
        """);

    [Fact]
    public void Check_CaveatedTupleWithoutRuntimeContext_ReturnsCaveated()
    {
        var tuple = new RelationTuple(
            new ObjectRef("document", "1"),
            "viewer",
            new SubjectRef("user", "alice"),
            CaveatName: "match_region",
            CaveatContext: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["expected_regions"] = new List<string> { "eu-west-1" },
            });

        var evaluator = new LoomEvaluator(ViewerSchema, new InMemoryRelationTupleStore([tuple]), MatchRegionCaveat);

        var result = evaluator.Check(new ObjectRef("document", "1"), "view", new SubjectRef("user", "alice"));

        var caveated = Assert.IsType<CheckResult.Caveated>(result);
        Assert.Equal(["observed_region"], caveated.MissingVariables);
    }

    [Fact]
    public void Check_CaveatedTupleWithMatchingRuntimeContext_Permits()
    {
        var tuple = new RelationTuple(
            new ObjectRef("document", "1"),
            "viewer",
            new SubjectRef("user", "alice"),
            CaveatName: "match_region",
            CaveatContext: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["expected_regions"] = new List<string> { "eu-west-1" },
            });

        var evaluator = new LoomEvaluator(ViewerSchema, new InMemoryRelationTupleStore([tuple]), MatchRegionCaveat);

        var context = new Dictionary<string, object?>(StringComparer.Ordinal) { ["observed_region"] = "eu-west-1" };
        var result = evaluator.Check(new ObjectRef("document", "1"), "view", new SubjectRef("user", "alice"), context);

        Assert.IsType<CheckResult.Permit>(result);
    }

    [Fact]
    public void Check_CaveatedTupleWithNonMatchingRuntimeContext_Denies()
    {
        var tuple = new RelationTuple(
            new ObjectRef("document", "1"),
            "viewer",
            new SubjectRef("user", "alice"),
            CaveatName: "match_region",
            CaveatContext: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["expected_regions"] = new List<string> { "eu-west-1" },
            });

        var evaluator = new LoomEvaluator(ViewerSchema, new InMemoryRelationTupleStore([tuple]), MatchRegionCaveat);

        var context = new Dictionary<string, object?>(StringComparer.Ordinal) { ["observed_region"] = "ap-south-1" };
        var result = evaluator.Check(new ObjectRef("document", "1"), "view", new SubjectRef("user", "alice"), context);

        Assert.IsType<CheckResult.Deny>(result);
    }

    [Fact]
    public void Check_UnconditionalGrantAlongsideCaveatedTuple_StillPermits()
    {
        var caveatedTuple = new RelationTuple(
            new ObjectRef("document", "1"),
            "viewer",
            new SubjectRef("user", "alice"),
            CaveatName: "match_region",
            CaveatContext: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["expected_regions"] = new List<string> { "eu-west-1" },
            });

        var unconditionalTuple = new RelationTuple(
            new ObjectRef("document", "1"),
            "viewer",
            new SubjectRef("user", "alice"));

        var evaluator = new LoomEvaluator(
            ViewerSchema,
            new InMemoryRelationTupleStore([caveatedTuple, unconditionalTuple]),
            MatchRegionCaveat);

        var result = evaluator.Check(new ObjectRef("document", "1"), "view", new SubjectRef("user", "alice"));

        Assert.IsType<CheckResult.Permit>(result);
    }

    [Fact]
    public void Check_ExclusionWithCaveatedExcludedBranch_ReturnsCaveated()
    {
        var schema = SchemaParser.Parse(
            """
            definition user {}

            definition document {
                relation owner: user
                relation blocked: user
                permission edit = owner - blocked
            }
            """);

        var blockedTuple = new RelationTuple(
            new ObjectRef("document", "1"),
            "blocked",
            new SubjectRef("user", "alice"),
            CaveatName: "match_region",
            CaveatContext: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["expected_regions"] = new List<string> { "eu-west-1" },
            });

        var ownerTuple = new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice"));

        var evaluator = new LoomEvaluator(
            schema,
            new InMemoryRelationTupleStore([ownerTuple, blockedTuple]),
            MatchRegionCaveat);

        var result = evaluator.Check(new ObjectRef("document", "1"), "edit", new SubjectRef("user", "alice"));

        var caveated = Assert.IsType<CheckResult.Caveated>(result);
        Assert.Equal(["observed_region"], caveated.MissingVariables);
    }
}
