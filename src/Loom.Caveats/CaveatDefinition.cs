namespace Loom.Caveats;

public sealed class CaveatDefinition(string name, IReadOnlyList<CaveatParameter> parameters, CelExpr body)
{
    public string Name { get; } = name;

    public IReadOnlyList<CaveatParameter> Parameters { get; } = parameters;

    public CelExpr Body { get; } = body;
}
