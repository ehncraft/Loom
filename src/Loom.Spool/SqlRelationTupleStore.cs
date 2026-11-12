using Loom.Core;
using Loom.Relationships;

namespace Loom.Spool;

public static class SqlRelationTupleStore
{
    public static IRelationTupleStore Create(SqlRelationTupleStoreOptions options) =>
        new SqlRelationTupleStoreImpl(options, SqlServerQueryExecutor.Create(options.ConnectionString));

    public static IRelationTupleStore Create(SqlRelationTupleStoreOptions options, ISqlQueryExecutor executor) =>
        new SqlRelationTupleStoreImpl(options, executor);
}

internal sealed class SqlRelationTupleStoreImpl(SqlRelationTupleStoreOptions options, ISqlQueryExecutor executor) : IRelationTupleStore
{
    private string Table => SqlIdentifier.Quote(options.Schema, options.Table);

    public async Task WriteAsync(RelationTuple tuple, CancellationToken cancellationToken)
    {
        var sql = $"""
            IF NOT EXISTS (
                SELECT 1 FROM {Table}
                WHERE {Col(options.ObjectNamespaceColumn)} = @objectNamespace
                  AND {Col(options.ObjectIdColumn)} = @objectId
                  AND {Col(options.RelationColumn)} = @relation
                  AND {Col(options.SubjectNamespaceColumn)} = @subjectNamespace
                  AND {Col(options.SubjectIdColumn)} = @subjectId
                  AND {Col(options.SubjectRelationColumn)} = @subjectRelation
                  AND {Col(options.CaveatNameColumn)} = @caveatName
            )
            BEGIN
                INSERT INTO {Table}
                    ({Col(options.ObjectNamespaceColumn)}, {Col(options.ObjectIdColumn)}, {Col(options.RelationColumn)},
                     {Col(options.SubjectNamespaceColumn)}, {Col(options.SubjectIdColumn)}, {Col(options.SubjectRelationColumn)},
                     {Col(options.CaveatNameColumn)}, {Col(options.CaveatContextColumn)})
                VALUES
                    (@objectNamespace, @objectId, @relation, @subjectNamespace, @subjectId, @subjectRelation,
                     @caveatName, @caveatContext)
            END
            """;

        await executor.ExecuteAsync(sql, Parameters(tuple), cancellationToken);
    }

    public async Task DeleteAsync(RelationTuple tuple, CancellationToken cancellationToken)
    {
        var sql = $"""
            DELETE FROM {Table}
            WHERE {Col(options.ObjectNamespaceColumn)} = @objectNamespace
              AND {Col(options.ObjectIdColumn)} = @objectId
              AND {Col(options.RelationColumn)} = @relation
              AND {Col(options.SubjectNamespaceColumn)} = @subjectNamespace
              AND {Col(options.SubjectIdColumn)} = @subjectId
              AND {Col(options.SubjectRelationColumn)} = @subjectRelation
              AND {Col(options.CaveatNameColumn)} = @caveatName
            """;

        await executor.ExecuteAsync(sql, Parameters(tuple), cancellationToken);
    }

    public async Task<IReadOnlyList<RelationTuple>> ReadAsync(
        ObjectRef resource, string relation, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {Col(options.ObjectNamespaceColumn)}, {Col(options.ObjectIdColumn)}, {Col(options.RelationColumn)},
                   {Col(options.SubjectNamespaceColumn)}, {Col(options.SubjectIdColumn)}, {Col(options.SubjectRelationColumn)},
                   {Col(options.CaveatNameColumn)}, {Col(options.CaveatContextColumn)}
            FROM {Table}
            WHERE {Col(options.ObjectNamespaceColumn)} = @objectNamespace
              AND {Col(options.ObjectIdColumn)} = @objectId
              AND {Col(options.RelationColumn)} = @relation
            """;

        var parameters = new Dictionary<string, object?>
        {
            ["@objectNamespace"] = resource.Namespace,
            ["@objectId"] = resource.Id,
            ["@relation"] = relation,
        };

        var rows = await executor.QueryAsync(sql, parameters, cancellationToken);
        return [.. rows.Select(ToTuple)];
    }

    public async Task<IReadOnlyList<ObjectRef>> ResourcesOfNamespaceAsync(
        string namespaceName, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT DISTINCT {Col(options.ObjectNamespaceColumn)}, {Col(options.ObjectIdColumn)}
            FROM {Table}
            WHERE {Col(options.ObjectNamespaceColumn)} = @namespace
            """;

        var rows = await executor.QueryAsync(
            sql, new Dictionary<string, object?> { ["@namespace"] = namespaceName }, cancellationToken);
        return [.. rows.Select(row => new ObjectRef(
            (string)row[options.ObjectNamespaceColumn]!,
            (string)row[options.ObjectIdColumn]!))];
    }

    public async Task<IReadOnlyList<SubjectRef>> SubjectsOfNamespaceAsync(
        string namespaceName, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT DISTINCT {Col(options.SubjectNamespaceColumn)}, {Col(options.SubjectIdColumn)}
            FROM {Table}
            WHERE {Col(options.SubjectNamespaceColumn)} = @namespace
              AND {Col(options.SubjectRelationColumn)} = ''
            """;

        var rows = await executor.QueryAsync(
            sql, new Dictionary<string, object?> { ["@namespace"] = namespaceName }, cancellationToken);
        return [.. rows.Select(row => new SubjectRef(
            (string)row[options.SubjectNamespaceColumn]!,
            (string)row[options.SubjectIdColumn]!))];
    }

    private static Dictionary<string, object?> Parameters(RelationTuple tuple) => new()
    {
        ["@objectNamespace"] = tuple.Resource.Namespace,
        ["@objectId"] = tuple.Resource.Id,
        ["@relation"] = tuple.Relation,
        ["@subjectNamespace"] = tuple.Subject.Namespace,
        ["@subjectId"] = tuple.Subject.Id,
        ["@subjectRelation"] = tuple.Subject.Relation ?? string.Empty,
        ["@caveatName"] = tuple.CaveatName ?? string.Empty,
        ["@caveatContext"] = CaveatContextJson.Serialize(tuple.CaveatContext),
    };

    private RelationTuple ToTuple(IReadOnlyDictionary<string, object?> row)
    {
        var subjectRelation = (string?)row[options.SubjectRelationColumn];
        var caveatName = (string?)row[options.CaveatNameColumn];

        return new RelationTuple(
            new ObjectRef((string)row[options.ObjectNamespaceColumn]!, (string)row[options.ObjectIdColumn]!),
            (string)row[options.RelationColumn]!,
            new SubjectRef(
                (string)row[options.SubjectNamespaceColumn]!,
                (string)row[options.SubjectIdColumn]!,
                string.IsNullOrEmpty(subjectRelation) ? null : subjectRelation),
            string.IsNullOrEmpty(caveatName) ? null : caveatName,
            CaveatContextJson.Deserialize((string?)row[options.CaveatContextColumn]));
    }

    private static string Col(string name) => SqlIdentifier.Quote(name);
}
