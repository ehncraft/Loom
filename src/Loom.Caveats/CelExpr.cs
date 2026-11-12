namespace Loom.Caveats;

public closed record CelExpr
{
    public sealed record Literal(object? Value) : CelExpr;

    public sealed record Variable(string Name) : CelExpr;

    public sealed record LogicalNot(CelExpr Operand) : CelExpr;

    public sealed record LogicalAnd(CelExpr Left, CelExpr Right) : CelExpr;

    public sealed record LogicalOr(CelExpr Left, CelExpr Right) : CelExpr;

    public sealed record Comparison(CelExpr Left, ComparisonOperator Operator, CelExpr Right) : CelExpr;

    public sealed record InList(CelExpr Item, CelExpr List) : CelExpr;
}
