namespace Loom.Spool;

public sealed class SqlRelationTupleStoreOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public required string Schema { get; set; }

    public string Table { get; set; } = "RelationTuples";

    public string ObjectNamespaceColumn { get; set; } = "ObjectNamespace";

    public string ObjectIdColumn { get; set; } = "ObjectId";

    public string RelationColumn { get; set; } = "Relation";

    public string SubjectNamespaceColumn { get; set; } = "SubjectNamespace";

    public string SubjectIdColumn { get; set; } = "SubjectId";

    public string SubjectRelationColumn { get; set; } = "SubjectRelation";

    public string CaveatNameColumn { get; set; } = "CaveatName";

    public string CaveatContextColumn { get; set; } = "CaveatContext";
}
