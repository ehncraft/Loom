namespace Loom.Schema;

public closed record PermissionExpr
{
    public sealed record Leaf(string Name) : PermissionExpr;

    public sealed record Arrow(string RelationName, string Target) : PermissionExpr;

    public sealed record Union(PermissionExpr Left, PermissionExpr Right) : PermissionExpr;

    public sealed record Intersection(PermissionExpr Left, PermissionExpr Right) : PermissionExpr;

    public sealed record Exclusion(PermissionExpr Left, PermissionExpr Right) : PermissionExpr;
}
