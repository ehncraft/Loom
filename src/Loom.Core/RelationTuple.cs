namespace Loom.Core;

public readonly record struct RelationTuple(ObjectRef Resource, string Relation, SubjectRef Subject)
{
    public override string ToString() => $"{Resource}#{Relation}@{Subject}";
}
