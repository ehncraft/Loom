namespace Loom.Caveats;

public closed record CaveatEvalResult
{
    public sealed record Satisfied(bool Value) : CaveatEvalResult;

    public sealed record MissingContext(IReadOnlyList<string> MissingVariables) : CaveatEvalResult;
}
