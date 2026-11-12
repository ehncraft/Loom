namespace Loom.Core;

public closed record CheckResult
{
    public sealed record Permit : CheckResult;

    public sealed record Deny : CheckResult;

    public sealed record Caveated(IReadOnlyList<string> MissingVariables) : CheckResult;
}
