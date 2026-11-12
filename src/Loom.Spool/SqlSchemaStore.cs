using Loom.Schema;

namespace Loom.Spool;

public static class SqlSchemaStore
{
    public static ISqlSchemaStore Create(SqlSchemaStoreOptions options) =>
        new SqlSchemaStoreImpl(options, SqlServerQueryExecutor.Create(options.ConnectionString));

    public static ISqlSchemaStore Create(SqlSchemaStoreOptions options, ISqlQueryExecutor executor) =>
        new SqlSchemaStoreImpl(options, executor);
}

internal sealed class SqlSchemaStoreImpl(SqlSchemaStoreOptions options, ISqlQueryExecutor executor) : ISqlSchemaStore
{
    private string Table => SqlIdentifier.Quote(options.Schema, options.Table);

    public async Task<LoomSchema?> LoadAsync(CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT TOP (1) {SqlIdentifier.Quote(options.SchemaTextColumn)}
            FROM {Table}
            ORDER BY {SqlIdentifier.Quote(options.UpdatedAtColumn)} DESC
            """;

        var rows = await executor.QueryAsync(sql, new Dictionary<string, object?>(), cancellationToken);
        if (rows.Count == 0)
        {
            return null;
        }

        var text = (string?)rows[0][options.SchemaTextColumn];
        return text is null ? null : SchemaParser.Parse(text);
    }

    public async Task SaveAsync(string schemaText, CancellationToken cancellationToken)
    {
        await executor.ExecuteAsync($"DELETE FROM {Table}", new Dictionary<string, object?>(), cancellationToken);

        var insertSql = $"""
            INSERT INTO {Table} ({SqlIdentifier.Quote(options.SchemaTextColumn)}, {SqlIdentifier.Quote(options.UpdatedAtColumn)})
            VALUES (@schemaText, SYSUTCDATETIME())
            """;

        await executor.ExecuteAsync(
            insertSql, new Dictionary<string, object?> { ["@schemaText"] = schemaText }, cancellationToken);
    }
}
