using Loom.Core;
using Loom.Relationships;
using Loom.Spool;

namespace Loom.Tests;

public class SqlRelationTupleStoreTests
{
    private static SqlRelationTupleStoreOptions Options() => new() { Schema = "tenant_1" };

    [Fact]
    public async Task WriteAsync_PlainTuple_UsesEmptyStringSentinelsNotNull()
    {
        var executor = new FakeSqlQueryExecutor();
        IRelationTupleStore store = SqlRelationTupleStore.Create(Options(), executor);

        await store.WriteAsync(
            new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice")),
            CancellationToken.None);

        Assert.Single(executor.ExecuteCalls);
        var (sql, parameters) = executor.ExecuteCalls[0];
        Assert.Contains("[tenant_1].[RelationTuples]", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO", sql, StringComparison.Ordinal);
        Assert.Equal("document", parameters["@objectNamespace"]);
        Assert.Equal("alice", parameters["@subjectId"]);
        Assert.Equal(string.Empty, parameters["@subjectRelation"]);
        Assert.Equal(string.Empty, parameters["@caveatName"]);
        Assert.Null(parameters["@caveatContext"]);
    }

    [Fact]
    public async Task DeleteAsync_ProducesDeleteStatementWithMatchingParameters()
    {
        var executor = new FakeSqlQueryExecutor();
        IRelationTupleStore store = SqlRelationTupleStore.Create(Options(), executor);
        var tuple = new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice"));

        await store.DeleteAsync(tuple, CancellationToken.None);

        Assert.Single(executor.ExecuteCalls);
        var (sql, parameters) = executor.ExecuteCalls[0];
        Assert.Contains("DELETE FROM", sql, StringComparison.Ordinal);
        Assert.Equal("owner", parameters["@relation"]);
    }

    [Fact]
    public async Task ReadAsync_MapsEmptyStringSentinelsBackToNull()
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ObjectNamespace"] = "document",
            ["ObjectId"] = "1",
            ["Relation"] = "owner",
            ["SubjectNamespace"] = "user",
            ["SubjectId"] = "alice",
            ["SubjectRelation"] = string.Empty,
            ["CaveatName"] = string.Empty,
            ["CaveatContext"] = null,
        };

        var executor = new FakeSqlQueryExecutor([row]);
        IRelationTupleStore store = SqlRelationTupleStore.Create(Options(), executor);

        var results = await store.ReadAsync(new ObjectRef("document", "1"), "owner", CancellationToken.None);

        var tuple = Assert.Single(results);
        Assert.Null(tuple.Subject.Relation);
        Assert.Null(tuple.CaveatName);
        Assert.Null(tuple.CaveatContext);
    }

    [Fact]
    public async Task WriteThenRead_RoundTripsCaveatNameAndContextThroughJson()
    {
        var tuple = new RelationTuple(
            new ObjectRef("document", "1"),
            "viewer",
            new SubjectRef("user", "alice"),
            CaveatName: "match_region",
            CaveatContext: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["expected_regions"] = new List<string> { "eu-west-1", "us-east-1" },
                ["min_score"] = 42,
                ["is_active"] = true,
            });

        var writeExecutor = new FakeSqlQueryExecutor();
        IRelationTupleStore writeStore = SqlRelationTupleStore.Create(Options(), writeExecutor);
        await writeStore.WriteAsync(tuple, CancellationToken.None);

        var writtenParameters = writeExecutor.ExecuteCalls[0].Parameters;
        var caveatContextJson = Assert.IsType<string>(writtenParameters["@caveatContext"]);

        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ObjectNamespace"] = "document",
            ["ObjectId"] = "1",
            ["Relation"] = "viewer",
            ["SubjectNamespace"] = "user",
            ["SubjectId"] = "alice",
            ["SubjectRelation"] = string.Empty,
            ["CaveatName"] = "match_region",
            ["CaveatContext"] = caveatContextJson,
        };

        var readExecutor = new FakeSqlQueryExecutor([row]);
        IRelationTupleStore readStore = SqlRelationTupleStore.Create(Options(), readExecutor);
        var results = await readStore.ReadAsync(new ObjectRef("document", "1"), "viewer", CancellationToken.None);

        var roundTripped = Assert.Single(results);
        Assert.Equal("match_region", roundTripped.CaveatName);
        Assert.NotNull(roundTripped.CaveatContext);
        Assert.Equal(42L, (long)roundTripped.CaveatContext["min_score"]!);
        Assert.True((bool)roundTripped.CaveatContext["is_active"]!);
        var regions = Assert.IsType<List<object?>>(roundTripped.CaveatContext["expected_regions"]);
        Assert.Equal(["eu-west-1", "us-east-1"], regions);
    }

    [Fact]
    public async Task ResourcesOfNamespaceAsync_MapsDistinctRows()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["ObjectNamespace"] = "document", ["ObjectId"] = "1" },
            new(StringComparer.Ordinal) { ["ObjectNamespace"] = "document", ["ObjectId"] = "2" },
        };

        var executor = new FakeSqlQueryExecutor(rows);
        IRelationTupleStore store = SqlRelationTupleStore.Create(Options(), executor);

        var resources = await store.ResourcesOfNamespaceAsync("document", CancellationToken.None);

        Assert.Equal([new ObjectRef("document", "1"), new ObjectRef("document", "2")], resources);
    }

    [Fact]
    public async Task SubjectsOfNamespaceAsync_FiltersOutUsersetReferencesInQuery()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["SubjectNamespace"] = "user", ["SubjectId"] = "alice" },
        };

        var executor = new FakeSqlQueryExecutor(rows);
        IRelationTupleStore store = SqlRelationTupleStore.Create(Options(), executor);

        var subjects = await store.SubjectsOfNamespaceAsync("user", CancellationToken.None);

        Assert.Equal([new SubjectRef("user", "alice")], subjects);
        Assert.Contains("SubjectRelation", executor.QueryCalls[0].CommandText, StringComparison.Ordinal);
    }
}
