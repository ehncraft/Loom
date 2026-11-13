namespace Loom.AuthZen;

public sealed class AuthZenEvaluationsResponse
{
    public required IReadOnlyList<AuthZenEvaluationResponse> Evaluations { get; init; }
}
