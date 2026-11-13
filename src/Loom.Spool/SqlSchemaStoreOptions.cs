namespace Loom.Spool;

public sealed class SqlSchemaStoreOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public required string Schema { get; set; }

    public string Table { get; set; } = "Schemas";

    public string SchemaTextColumn { get; set; } = "SchemaText";

    public string UpdatedAtColumn { get; set; } = "UpdatedAt";
}
