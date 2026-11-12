namespace Loom.Schema;

public readonly record struct RelationTypeRef(string Namespace, string? Relation = null)
{
    public override string ToString() =>
        Relation is null ? Namespace : $"{Namespace}#{Relation}";
}
