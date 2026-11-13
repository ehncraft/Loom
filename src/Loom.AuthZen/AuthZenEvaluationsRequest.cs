using System.Text.Json;

namespace Loom.AuthZen;

public sealed class AuthZenEvaluationsRequest
{
    public AuthZenEntity? Subject { get; init; }

    public AuthZenEntity? Resource { get; init; }

    public AuthZenAction? Action { get; init; }

    public Dictionary<string, JsonElement>? Context { get; init; }

    public required IReadOnlyList<AuthZenEvaluationItem> Evaluations { get; init; }

    public AuthZenEvaluationsOptions? Options { get; init; }
}

public sealed class AuthZenEvaluationItem
{
    public AuthZenEntity? Subject { get; init; }

    public AuthZenEntity? Resource { get; init; }

    public AuthZenAction? Action { get; init; }

    public Dictionary<string, JsonElement>? Context { get; init; }
}

public sealed class AuthZenEvaluationsOptions
{
    public string? EvaluationsSemantic { get; init; }
}
