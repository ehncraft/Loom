using Loom.Core;
using Loom.Evaluator;

namespace Loom.Testing;

public static class LoomEvaluatorAssertions
{
    public static async Task ShouldPermitAsync(
        this LoomEvaluator evaluator,
        ObjectRef resource,
        string permissionOrRelation,
        SubjectRef subject,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        var result = await evaluator.CheckAsync(resource, permissionOrRelation, subject, context, cancellationToken);
        if (result is not CheckResult.Permit)
        {
            throw new LoomAssertionException(
                $"Expected {subject} to be permitted '{permissionOrRelation}' on {resource}, but got {Describe(result)}.");
        }
    }

    public static async Task ShouldDenyAsync(
        this LoomEvaluator evaluator,
        ObjectRef resource,
        string permissionOrRelation,
        SubjectRef subject,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        var result = await evaluator.CheckAsync(resource, permissionOrRelation, subject, context, cancellationToken);
        if (result is not CheckResult.Deny)
        {
            throw new LoomAssertionException(
                $"Expected {subject} to be denied '{permissionOrRelation}' on {resource}, but got {Describe(result)}.");
        }
    }

    public static async Task ShouldRequireCaveatContextAsync(
        this LoomEvaluator evaluator,
        ObjectRef resource,
        string permissionOrRelation,
        SubjectRef subject,
        params string[] expectedMissingVariables)
    {
        var result = await evaluator.CheckAsync(resource, permissionOrRelation, subject);
        if (result is not CheckResult.Caveated caveated)
        {
            throw new LoomAssertionException(
                $"Expected {subject}'s '{permissionOrRelation}' check on {resource} to require more context, " +
                $"but got {Describe(result)}.");
        }

        if (expectedMissingVariables.Length > 0 &&
            !expectedMissingVariables.OrderBy(v => v, StringComparer.Ordinal)
                .SequenceEqual(caveated.MissingVariables.OrderBy(v => v, StringComparer.Ordinal)))
        {
            throw new LoomAssertionException(
                $"Expected missing variables [{string.Join(", ", expectedMissingVariables)}] but got " +
                $"[{string.Join(", ", caveated.MissingVariables)}].");
        }
    }

    private static string Describe(CheckResult result) => result switch
    {
        CheckResult.Permit => "Permit",
        CheckResult.Deny => "Deny",
        CheckResult.Caveated caveated => $"Caveated (missing: {string.Join(", ", caveated.MissingVariables)})",
        _ => result.GetType().Name,
    };
}
