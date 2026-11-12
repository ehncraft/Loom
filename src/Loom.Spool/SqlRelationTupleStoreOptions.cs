namespace Loom.Spool;

/// <summary>
/// Maps <see cref="SqlRelationTupleStore"/> onto a table holding every relation tuple for one
/// tenant. Multi-tenancy is structural via <see cref="Schema"/> (one schema per tenant, e.g.
/// <c>tenant_123.RelationTuples</c>) and a separate store instance per tenant -- not a shared
/// table filtered by a tenant-id column -- the same convention Aegis.Sql uses for its own
/// per-tenant Cedar policy tables.
/// </summary>
public sealed class SqlRelationTupleStoreOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Deliberately <c>required</c>, with no default -- SQL Server resolves an unqualified table
    /// name against the connecting login's own default schema, which isn't necessarily
    /// <c>dbo</c> and can silently differ per login/connection.
    /// </summary>
    public required string Schema { get; set; }

    public string Table { get; set; } = "RelationTuples";

    public string ObjectNamespaceColumn { get; set; } = "ObjectNamespace";

    public string ObjectIdColumn { get; set; } = "ObjectId";

    public string RelationColumn { get; set; } = "Relation";

    public string SubjectNamespaceColumn { get; set; } = "SubjectNamespace";

    public string SubjectIdColumn { get; set; } = "SubjectId";

    /// <summary>Empty string (not NULL) means "no subject relation" -- a plain subject rather
    /// than a userset reference -- so the column can participate in the table's primary key.</summary>
    public string SubjectRelationColumn { get; set; } = "SubjectRelation";

    /// <summary>Empty string (not NULL) means "no caveat attached", same NOT NULL convention as
    /// <see cref="SubjectRelationColumn"/>.</summary>
    public string CaveatNameColumn { get; set; } = "CaveatName";

    /// <summary>JSON-serialized caveat context bound at write time (merged with request-time
    /// context during <c>Check</c>). NULL when no caveat is attached.</summary>
    public string CaveatContextColumn { get; set; } = "CaveatContext";
}
