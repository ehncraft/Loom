namespace Loom.Core;

public readonly record struct SubjectRef(string Namespace, string Id, string? Relation = null)
{
    public ObjectRef AsObjectRef() => new(Namespace, Id);

    public override string ToString() =>
        Relation is null ? $"{Namespace}:{Id}" : $"{Namespace}:{Id}#{Relation}";
}
