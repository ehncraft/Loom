using System.Text.Json;

namespace Loom.AuthZen;

public sealed class AuthZenEvaluationRequest
{
    public required AuthZenEntity Subject { get; init; }

    public required AuthZenEntity Resource { get; init; }

    public required AuthZenAction Action { get; init; }

    public Dictionary<string, JsonElement>? Context { get; init; }
}
