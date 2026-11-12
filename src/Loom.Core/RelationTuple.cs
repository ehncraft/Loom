namespace Loom.Core;

public readonly record struct RelationTuple(
    ObjectRef Resource,
    string Relation,
    SubjectRef Subject,
    string? CaveatName = null,
    IReadOnlyDictionary<string, object?>? CaveatContext = null)
{
    public override string ToString() =>
        CaveatName is null ? $"{Resource}#{Relation}@{Subject}" : $"{Resource}#{Relation}@{Subject}[{CaveatName}]";
}
