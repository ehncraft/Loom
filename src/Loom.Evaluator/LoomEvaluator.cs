using Loom.Caveats;
using Loom.Core;
using Loom.Relationships;
using Loom.Schema;

namespace Loom.Evaluator;

public sealed class LoomEvaluator(
    LoomSchema schema,
    IRelationTupleReader tuples,
    IReadOnlyDictionary<string, CaveatDefinition>? caveats = null)
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyContext =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<string, CaveatDefinition> _caveats =
        caveats ?? new Dictionary<string, CaveatDefinition>(StringComparer.Ordinal);

    public Task<CheckResult> CheckAsync(
        ObjectRef resource,
        string permissionOrRelation,
        SubjectRef subject,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        var inProgress = new HashSet<(string Namespace, string Id, string Member)>();
        return CheckMemberAsync(resource, permissionOrRelation, subject, context ?? EmptyContext, inProgress, cancellationToken);
    }

    public Task<ExpandNode> ExpandAsync(
        ObjectRef resource,
        string permissionOrRelation,
        CancellationToken cancellationToken = default)
    {
        var inProgress = new HashSet<(string Namespace, string Id, string Member)>();
        return ExpandMemberAsync(resource, permissionOrRelation, inProgress, cancellationToken);
    }

    public async Task<IReadOnlyList<ObjectRef>> LookupResourcesAsync(
        SubjectRef subject,
        string permission,
        string resourceNamespace,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = await tuples.ResourcesOfNamespaceAsync(resourceNamespace, cancellationToken);
        var results = new List<ObjectRef>();
        foreach (var resource in candidates)
        {
            if (await CheckAsync(resource, permission, subject, context, cancellationToken) is CheckResult.Permit)
            {
                results.Add(resource);
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<SubjectRef>> LookupSubjectsAsync(
        ObjectRef resource,
        string permission,
        string subjectNamespace,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = await tuples.SubjectsOfNamespaceAsync(subjectNamespace, cancellationToken);
        var results = new List<SubjectRef>();
        foreach (var subject in candidates)
        {
            if (await CheckAsync(resource, permission, subject, context, cancellationToken) is CheckResult.Permit)
            {
                results.Add(subject);
            }
        }

        return results;
    }

    private async Task<CheckResult> CheckMemberAsync(
        ObjectRef resource,
        string member,
        SubjectRef subject,
        IReadOnlyDictionary<string, object?> context,
        HashSet<(string Namespace, string Id, string Member)> inProgress,
        CancellationToken cancellationToken)
    {
        var key = (resource.Namespace, resource.Id, member);
        if (!inProgress.Add(key))
        {
            return new CheckResult.Deny();
        }

        try
        {
            if (!schema.TryGetType(resource.Namespace, out var type) || type is null)
            {
                return new CheckResult.Deny();
            }

            if (type.Permissions.TryGetValue(member, out var permission))
            {
                return await EvaluateExprAsync(permission.Expression, resource, subject, context, inProgress, cancellationToken);
            }

            if (type.Relations.TryGetValue(member, out _))
            {
                return await CheckRelationTuplesAsync(resource, member, subject, context, inProgress, cancellationToken);
            }

            return new CheckResult.Deny();
        }
        finally
        {
            inProgress.Remove(key);
        }
    }

    private async Task<CheckResult> CheckRelationTuplesAsync(
        ObjectRef resource,
        string relation,
        SubjectRef subject,
        IReadOnlyDictionary<string, object?> context,
        HashSet<(string Namespace, string Id, string Member)> inProgress,
        CancellationToken cancellationToken)
    {
        HashSet<string>? missing = null;

        foreach (var tuple in await tuples.ReadAsync(resource, relation, cancellationToken))
        {
            CheckResult outcome;
            if (tuple.Subject.Relation is null)
            {
                if (tuple.Subject.Namespace != subject.Namespace || tuple.Subject.Id != subject.Id)
                {
                    continue;
                }

                outcome = EvaluateTupleCaveat(tuple, context);
            }
            else
            {
                var nested = await CheckMemberAsync(
                    tuple.Subject.AsObjectRef(), tuple.Subject.Relation, subject, context, inProgress, cancellationToken);
                outcome = GateByTupleCaveat(tuple, nested, context);
            }

            if (outcome is CheckResult.Permit)
            {
                return outcome;
            }

            if (outcome is CheckResult.Caveated caveated)
            {
                missing ??= new HashSet<string>(StringComparer.Ordinal);
                foreach (var variable in caveated.MissingVariables)
                {
                    missing.Add(variable);
                }
            }
        }

        return missing is { Count: > 0 } ? new CheckResult.Caveated([.. missing]) : new CheckResult.Deny();
    }

    private async Task<CheckResult> EvaluateExprAsync(
        PermissionExpr expr,
        ObjectRef resource,
        SubjectRef subject,
        IReadOnlyDictionary<string, object?> context,
        HashSet<(string Namespace, string Id, string Member)> inProgress,
        CancellationToken cancellationToken) => expr switch
        {
            PermissionExpr.Leaf leaf => await CheckMemberAsync(resource, leaf.Name, subject, context, inProgress, cancellationToken),
            PermissionExpr.Arrow arrow => await EvaluateArrowAsync(arrow, resource, subject, context, inProgress, cancellationToken),
            PermissionExpr.Union union => Or(
                await EvaluateExprAsync(union.Left, resource, subject, context, inProgress, cancellationToken),
                await EvaluateExprAsync(union.Right, resource, subject, context, inProgress, cancellationToken)),
            PermissionExpr.Intersection intersection => And(
                await EvaluateExprAsync(intersection.Left, resource, subject, context, inProgress, cancellationToken),
                await EvaluateExprAsync(intersection.Right, resource, subject, context, inProgress, cancellationToken)),
            PermissionExpr.Exclusion exclusion => Exclude(
                await EvaluateExprAsync(exclusion.Left, resource, subject, context, inProgress, cancellationToken),
                await EvaluateExprAsync(exclusion.Right, resource, subject, context, inProgress, cancellationToken)),
            _ => new CheckResult.Deny(),
        };

    private async Task<CheckResult> EvaluateArrowAsync(
        PermissionExpr.Arrow arrow,
        ObjectRef resource,
        SubjectRef subject,
        IReadOnlyDictionary<string, object?> context,
        HashSet<(string Namespace, string Id, string Member)> inProgress,
        CancellationToken cancellationToken)
    {
        HashSet<string>? missing = null;

        foreach (var tuple in await tuples.ReadAsync(resource, arrow.RelationName, cancellationToken))
        {
            var parent = tuple.Subject.AsObjectRef();
            var nested = await CheckMemberAsync(parent, arrow.Target, subject, context, inProgress, cancellationToken);
            var outcome = GateByTupleCaveat(tuple, nested, context);

            if (outcome is CheckResult.Permit)
            {
                return outcome;
            }

            if (outcome is CheckResult.Caveated caveated)
            {
                missing ??= new HashSet<string>(StringComparer.Ordinal);
                foreach (var variable in caveated.MissingVariables)
                {
                    missing.Add(variable);
                }
            }
        }

        return missing is { Count: > 0 } ? new CheckResult.Caveated([.. missing]) : new CheckResult.Deny();
    }

    private CheckResult EvaluateTupleCaveat(RelationTuple tuple, IReadOnlyDictionary<string, object?> context)
    {
        if (tuple.CaveatName is null)
        {
            return new CheckResult.Permit();
        }

        if (!_caveats.TryGetValue(tuple.CaveatName, out var definition))
        {
            return new CheckResult.Deny();
        }

        var merged = MergeContext(tuple.CaveatContext, context);
        return CaveatEvaluator.Evaluate(definition, merged) switch
        {
            CaveatEvalResult.Satisfied { Value: true } => new CheckResult.Permit(),
            CaveatEvalResult.Satisfied { Value: false } => new CheckResult.Deny(),
            CaveatEvalResult.MissingContext missingContext => new CheckResult.Caveated(missingContext.MissingVariables),
            _ => new CheckResult.Deny(),
        };
    }

    private CheckResult GateByTupleCaveat(RelationTuple tuple, CheckResult nested, IReadOnlyDictionary<string, object?> context)
    {
        if (nested is CheckResult.Deny)
        {
            return nested;
        }

        var tupleCaveat = EvaluateTupleCaveat(tuple, context);
        return And(tupleCaveat, nested);
    }

    private static IReadOnlyDictionary<string, object?> MergeContext(
        IReadOnlyDictionary<string, object?>? tupleContext,
        IReadOnlyDictionary<string, object?> requestContext)
    {
        if (tupleContext is null || tupleContext.Count == 0)
        {
            return requestContext;
        }

        var merged = new Dictionary<string, object?>(tupleContext, StringComparer.Ordinal);
        foreach (var (key, value) in requestContext)
        {
            merged[key] = value;
        }

        return merged;
    }

    private static CheckResult Or(CheckResult left, CheckResult right)
    {
        if (left is CheckResult.Permit || right is CheckResult.Permit)
        {
            return new CheckResult.Permit();
        }

        if (left is CheckResult.Deny && right is CheckResult.Deny)
        {
            return new CheckResult.Deny();
        }

        return new CheckResult.Caveated(CollectMissing(left, right));
    }

    private static CheckResult And(CheckResult left, CheckResult right)
    {
        if (left is CheckResult.Deny || right is CheckResult.Deny)
        {
            return new CheckResult.Deny();
        }

        if (left is CheckResult.Permit && right is CheckResult.Permit)
        {
            return new CheckResult.Permit();
        }

        return new CheckResult.Caveated(CollectMissing(left, right));
    }

    private static CheckResult Exclude(CheckResult included, CheckResult excluded)
    {
        if (included is CheckResult.Deny)
        {
            return new CheckResult.Deny();
        }

        if (excluded is CheckResult.Permit)
        {
            return new CheckResult.Deny();
        }

        if (excluded is CheckResult.Deny)
        {
            return included;
        }

        var excludedMissing = ((CheckResult.Caveated)excluded).MissingVariables;
        return included is CheckResult.Permit
            ? new CheckResult.Caveated(excludedMissing)
            : new CheckResult.Caveated(CollectMissing(included, excluded));
    }

    private static IReadOnlyList<string> CollectMissing(CheckResult left, CheckResult right)
    {
        var missing = new HashSet<string>(StringComparer.Ordinal);
        if (left is CheckResult.Caveated leftCaveated)
        {
            foreach (var variable in leftCaveated.MissingVariables)
            {
                missing.Add(variable);
            }
        }

        if (right is CheckResult.Caveated rightCaveated)
        {
            foreach (var variable in rightCaveated.MissingVariables)
            {
                missing.Add(variable);
            }
        }

        return [.. missing];
    }

    private async Task<ExpandNode> ExpandMemberAsync(
        ObjectRef resource,
        string member,
        HashSet<(string Namespace, string Id, string Member)> inProgress,
        CancellationToken cancellationToken)
    {
        var key = (resource.Namespace, resource.Id, member);
        if (!inProgress.Add(key))
        {
            return new ExpandNode.Leaf([]);
        }

        try
        {
            if (!schema.TryGetType(resource.Namespace, out var type) || type is null)
            {
                return new ExpandNode.Leaf([]);
            }

            if (type.Permissions.TryGetValue(member, out var permission))
            {
                return await ExpandExprAsync(permission.Expression, resource, inProgress, cancellationToken);
            }

            if (type.Relations.TryGetValue(member, out _))
            {
                return await ExpandRelationAsync(resource, member, inProgress, cancellationToken);
            }

            return new ExpandNode.Leaf([]);
        }
        finally
        {
            inProgress.Remove(key);
        }
    }

    private async Task<ExpandNode> ExpandRelationAsync(
        ObjectRef resource,
        string relation,
        HashSet<(string Namespace, string Id, string Member)> inProgress,
        CancellationToken cancellationToken)
    {
        var direct = new List<SubjectRef>();
        var nested = new List<ExpandNode>();

        foreach (var tuple in await tuples.ReadAsync(resource, relation, cancellationToken))
        {
            if (tuple.Subject.Relation is null)
            {
                direct.Add(tuple.Subject);
            }
            else
            {
                nested.Add(await ExpandMemberAsync(tuple.Subject.AsObjectRef(), tuple.Subject.Relation, inProgress, cancellationToken));
            }
        }

        if (nested.Count == 0)
        {
            return new ExpandNode.Leaf(direct);
        }

        List<ExpandNode> children = [new ExpandNode.Leaf(direct), .. nested];
        return new ExpandNode.Union(children);
    }

    private async Task<ExpandNode> ExpandExprAsync(
        PermissionExpr expr,
        ObjectRef resource,
        HashSet<(string Namespace, string Id, string Member)> inProgress,
        CancellationToken cancellationToken) => expr switch
        {
            PermissionExpr.Leaf leaf => await ExpandMemberAsync(resource, leaf.Name, inProgress, cancellationToken),
            PermissionExpr.Arrow arrow => await ExpandArrowAsync(arrow, resource, inProgress, cancellationToken),
            PermissionExpr.Union union => new ExpandNode.Union([
                await ExpandExprAsync(union.Left, resource, inProgress, cancellationToken),
            await ExpandExprAsync(union.Right, resource, inProgress, cancellationToken),
        ]),
            PermissionExpr.Intersection intersection => new ExpandNode.Intersection([
                await ExpandExprAsync(intersection.Left, resource, inProgress, cancellationToken),
            await ExpandExprAsync(intersection.Right, resource, inProgress, cancellationToken),
        ]),
            PermissionExpr.Exclusion exclusion => new ExpandNode.Exclusion(
                await ExpandExprAsync(exclusion.Left, resource, inProgress, cancellationToken),
                await ExpandExprAsync(exclusion.Right, resource, inProgress, cancellationToken)),
            _ => new ExpandNode.Leaf([]),
        };

    private async Task<ExpandNode.Union> ExpandArrowAsync(
        PermissionExpr.Arrow arrow,
        ObjectRef resource,
        HashSet<(string Namespace, string Id, string Member)> inProgress,
        CancellationToken cancellationToken)
    {
        List<ExpandNode> children = [];
        foreach (var tuple in await tuples.ReadAsync(resource, arrow.RelationName, cancellationToken))
        {
            children.Add(await ExpandMemberAsync(tuple.Subject.AsObjectRef(), arrow.Target, inProgress, cancellationToken));
        }

        return new ExpandNode.Union(children);
    }
}
