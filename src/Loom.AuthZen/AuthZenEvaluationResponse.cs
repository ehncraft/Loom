namespace Loom.AuthZen;

public sealed class AuthZenEvaluationResponse
{
    public required bool Decision { get; init; }

    public Dictionary<string, object?>? Context { get; init; }
}
