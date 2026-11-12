namespace Loom.Core;

public closed record ExpandNode
{
    public sealed record Leaf(IReadOnlyList<SubjectRef> Subjects) : ExpandNode;

    public sealed record Union(IReadOnlyList<ExpandNode> Children) : ExpandNode;

    public sealed record Intersection(IReadOnlyList<ExpandNode> Children) : ExpandNode;

    public sealed record Exclusion(ExpandNode Included, ExpandNode Excluded) : ExpandNode;
}
