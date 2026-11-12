using Loom.Spool;

namespace Loom.Tests;

public class SqlSchemaStoreTests
{
    private static SqlSchemaStoreOptions Options() => new() { Schema = "tenant_1" };

    [Fact]
    public async Task LoadAsync_NoRows_ReturnsNull()
    {
        var executor = new FakeSqlQueryExecutor([]);
        var store = SqlSchemaStore.Create(Options(), executor);

        var schema = await store.LoadAsync(CancellationToken.None);

        Assert.Null(schema);
    }

    [Fact]
    public async Task LoadAsync_WithRow_ParsesSchemaText()
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["SchemaText"] = "definition user {}",
        };

        var executor = new FakeSqlQueryExecutor([row]);
        var store = SqlSchemaStore.Create(Options(), executor);

        var schema = await store.LoadAsync(CancellationToken.None);

        Assert.NotNull(schema);
        Assert.True(schema.TryGetType("user", out _));
    }

    [Fact]
    public async Task SaveAsync_DeletesThenInsertsSchemaText()
    {
        var executor = new FakeSqlQueryExecutor();
        var store = SqlSchemaStore.Create(Options(), executor);

        await store.SaveAsync("definition user {}", CancellationToken.None);

        Assert.Equal(2, executor.ExecuteCalls.Count);
        Assert.Contains("DELETE FROM", executor.ExecuteCalls[0].CommandText, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO", executor.ExecuteCalls[1].CommandText, StringComparison.Ordinal);
        Assert.Equal("definition user {}", executor.ExecuteCalls[1].Parameters["@schemaText"]);
    }
}
