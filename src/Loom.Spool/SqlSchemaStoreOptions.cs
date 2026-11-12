namespace Loom.Spool;

/// <summary>
/// Maps <see cref="SqlSchemaStore"/> onto a table holding one tenant's current schema DSL text.
/// Same per-tenant-schema multi-tenancy convention as
/// <see cref="SqlRelationTupleStoreOptions"/> -- one row per tenant (the latest save wins), not
/// a shared table filtered by a tenant-id column.
/// </summary>
public sealed class SqlSchemaStoreOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public required string Schema { get; set; }

    public string Table { get; set; } = "Schemas";

    public string SchemaTextColumn { get; set; } = "SchemaText";

    public string UpdatedAtColumn { get; set; } = "UpdatedAt";
}
