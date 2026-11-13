using System.Text.Json;

using Loom.Core;

namespace Loom.AuthZen;

internal static class AuthZenMapping
{
    public static SubjectRef ToSubjectRef(AuthZenEntity subject) => new(subject.Type, subject.Id);

    public static ObjectRef ToObjectRef(AuthZenEntity resource) => new(resource.Type, resource.Id);

    public static Dictionary<string, object?>? ToContext(Dictionary<string, JsonElement>? context) =>
        JsonValueConverter.ToClrValues(context);

    public static AuthZenEvaluationResponse ToResponse(CheckResult result) => result switch
    {
        CheckResult.Permit => new AuthZenEvaluationResponse { Decision = true },
        CheckResult.Deny => new AuthZenEvaluationResponse { Decision = false },
        CheckResult.Caveated caveated => new AuthZenEvaluationResponse
        {
            Decision = false,
            Context = new Dictionary<string, object?> { ["missing_context"] = caveated.MissingVariables },
        },
        _ => new AuthZenEvaluationResponse { Decision = false },
    };
}
