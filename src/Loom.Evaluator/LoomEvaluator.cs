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

    public CheckResult Check(
        ObjectRef resource,
        string permissionOrRelation,
        SubjectRef subject,
        IReadOnlyDictionary<string, object?>? context = null)
    {
        var inProgress = new HashSet<(string Namespace, string Id, string Member)>();
        return CheckMember(resource, permissionOrRelation, subject, context ?? EmptyContext, inProgress);
    }

    public ExpandNode Expand(ObjectRef resource, string permissionOrRelation)
    {
        var inProgress = new HashSet<(string Namespace, string Id, string Member)>();
        return ExpandMember(resource, permissionOrRelation, inProgress);
    }

    public IEnumerable<ObjectRef> LookupResources(
        SubjectRef subject,
        string permission,
        string resourceNamespace,
        IReadOnlyDictionary<string, object?>? context = null) =>
        tuples.ResourcesOfNamespace(resourceNamespace)
            .Where(resource => Check(resource, permission, subject, context) is CheckResult.Permit);

    public IEnumerable<SubjectRef> LookupSubjects(
        ObjectRef resource,
        string permission,
        string subjectNamespace,
        IReadOnlyDictionary<string, object?>? context = null) =>
        tuples.SubjectsOfNamespace(subjectNamespace)
            .Where(subject => Check(resource, permission, subject, context) is CheckResult.Permit);

    private CheckResult CheckMember(
        ObjectRef resource,
        string member,
        SubjectRef subject,
        IReadOnlyDictionary<string, object?> context,
        HashSet<(string Namespace, string Id, string Member)> inProgress)
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
                return EvaluateExpr(permission.Expression, resource, subject, context, inProgress);
            }

            if (type.Relations.TryGetValue(member, out _))
            {
                return CheckRelationTuples(resource, member, subject, context, inProgress);
            }

            return new CheckResult.Deny();
        }
        finally
        {
            inProgress.Remove(key);
        }
    }

    private CheckResult CheckRelationTuples(
        ObjectRef resource,
        string relation,
        SubjectRef subject,
        IReadOnlyDictionary<string, object?> context,
        HashSet<(string Namespace, string Id, string Member)> inProgress)
    {
        HashSet<string>? missing = null;

        foreach (var tuple in tuples.Read(resource, relation))
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
                var nested = CheckMember(tuple.Subject.AsObjectRef(), tuple.Subject.Relation, subject, context, inProgress);
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

    private CheckResult EvaluateExpr(
        PermissionExpr expr,
        ObjectRef resource,
        SubjectRef subject,
        IReadOnlyDictionary<string, object?> context,
        HashSet<(string Namespace, string Id, string Member)> inProgress) => expr switch
        {
            PermissionExpr.Leaf leaf => CheckMember(resource, leaf.Name, subject, context, inProgress),
            PermissionExpr.Arrow arrow => EvaluateArrow(arrow, resource, subject, context, inProgress),
            PermissionExpr.Union union => Or(
                EvaluateExpr(union.Left, resource, subject, context, inProgress),
                EvaluateExpr(union.Right, resource, subject, context, inProgress)),
            PermissionExpr.Intersection intersection => And(
                EvaluateExpr(intersection.Left, resource, subject, context, inProgress),
                EvaluateExpr(intersection.Right, resource, subject, context, inProgress)),
            PermissionExpr.Exclusion exclusion => Exclude(
                EvaluateExpr(exclusion.Left, resource, subject, context, inProgress),
                EvaluateExpr(exclusion.Right, resource, subject, context, inProgress)),
            _ => new CheckResult.Deny(),
        };

    private CheckResult EvaluateArrow(
        PermissionExpr.Arrow arrow,
        ObjectRef resource,
        SubjectRef subject,
        IReadOnlyDictionary<string, object?> context,
        HashSet<(string Namespace, string Id, string Member)> inProgress)
    {
        HashSet<string>? missing = null;

        foreach (var tuple in tuples.Read(resource, arrow.RelationName))
        {
            var parent = tuple.Subject.AsObjectRef();
            var nested = CheckMember(parent, arrow.Target, subject, context, inProgress);
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

    private ExpandNode ExpandMember(
        ObjectRef resource,
        string member,
        HashSet<(string Namespace, string Id, string Member)> inProgress)
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
                return ExpandExpr(permission.Expression, resource, inProgress);
            }

            if (type.Relations.TryGetValue(member, out _))
            {
                return ExpandRelation(resource, member, inProgress);
            }

            return new ExpandNode.Leaf([]);
        }
        finally
        {
            inProgress.Remove(key);
        }
    }

    private ExpandNode ExpandRelation(
        ObjectRef resource,
        string relation,
        HashSet<(string Namespace, string Id, string Member)> inProgress)
    {
        var direct = new List<SubjectRef>();
        var nested = new List<ExpandNode>();

        foreach (var tuple in tuples.Read(resource, relation))
        {
            if (tuple.Subject.Relation is null)
            {
                direct.Add(tuple.Subject);
            }
            else
            {
                nested.Add(ExpandMember(tuple.Subject.AsObjectRef(), tuple.Subject.Relation, inProgress));
            }
        }

        if (nested.Count == 0)
        {
            return new ExpandNode.Leaf(direct);
        }

        List<ExpandNode> children = [new ExpandNode.Leaf(direct), .. nested];
        return new ExpandNode.Union(children);
    }

    private ExpandNode ExpandExpr(
        PermissionExpr expr,
        ObjectRef resource,
        HashSet<(string Namespace, string Id, string Member)> inProgress) => expr switch
        {
            PermissionExpr.Leaf leaf => ExpandMember(resource, leaf.Name, inProgress),
            PermissionExpr.Arrow arrow => ExpandArrow(arrow, resource, inProgress),
            PermissionExpr.Union union => new ExpandNode.Union([
                ExpandExpr(union.Left, resource, inProgress),
            ExpandExpr(union.Right, resource, inProgress),
        ]),
            PermissionExpr.Intersection intersection => new ExpandNode.Intersection([
                ExpandExpr(intersection.Left, resource, inProgress),
            ExpandExpr(intersection.Right, resource, inProgress),
        ]),
            PermissionExpr.Exclusion exclusion => new ExpandNode.Exclusion(
                ExpandExpr(exclusion.Left, resource, inProgress),
                ExpandExpr(exclusion.Right, resource, inProgress)),
            _ => new ExpandNode.Leaf([]),
        };

    private ExpandNode.Union ExpandArrow(
        PermissionExpr.Arrow arrow,
        ObjectRef resource,
        HashSet<(string Namespace, string Id, string Member)> inProgress)
    {
        List<ExpandNode> children = [];
        foreach (var tuple in tuples.Read(resource, arrow.RelationName))
        {
            children.Add(ExpandMember(tuple.Subject.AsObjectRef(), arrow.Target, inProgress));
        }

        return new ExpandNode.Union(children);
    }
}
