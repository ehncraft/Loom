using Loom.Core;
using Loom.Schema;

namespace Loom.Evaluator;

public sealed class LoomEvaluator(LoomSchema schema, IRelationTupleReader tuples)
{
    public CheckResult Check(ObjectRef resource, string permissionOrRelation, SubjectRef subject)
    {
        var inProgress = new HashSet<(string Namespace, string Id, string Member)>();
        return CheckMember(resource, permissionOrRelation, subject, inProgress);
    }

    private CheckResult CheckMember(
        ObjectRef resource,
        string member,
        SubjectRef subject,
        HashSet<(string Namespace, string Id, string Member)> inProgress)
    {
        var key = (resource.Namespace, resource.Id, member);
        if (!inProgress.Add(key))
        {
            return CheckResult.Deny;
        }

        try
        {
            if (!schema.TryGetType(resource.Namespace, out var type) || type is null)
            {
                return CheckResult.Deny;
            }

            if (type.Permissions.TryGetValue(member, out var permission))
            {
                return EvaluateExpr(permission.Expression, resource, subject, inProgress);
            }

            if (type.Relations.TryGetValue(member, out _))
            {
                return CheckRelationTuples(resource, member, subject, inProgress);
            }

            return CheckResult.Deny;
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
        HashSet<(string Namespace, string Id, string Member)> inProgress)
    {
        foreach (var tuple in tuples.Read(resource, relation))
        {
            if (tuple.Subject.Relation is null)
            {
                if (tuple.Subject.Namespace == subject.Namespace && tuple.Subject.Id == subject.Id)
                {
                    return CheckResult.Permit;
                }

                continue;
            }

            if (CheckMember(tuple.Subject.AsObjectRef(), tuple.Subject.Relation, subject, inProgress) is CheckResult.Permit)
            {
                return CheckResult.Permit;
            }
        }

        return CheckResult.Deny;
    }

    private CheckResult EvaluateExpr(
        PermissionExpr expr,
        ObjectRef resource,
        SubjectRef subject,
        HashSet<(string Namespace, string Id, string Member)> inProgress) => expr switch
        {
            PermissionExpr.Leaf leaf => CheckMember(resource, leaf.Name, subject, inProgress),
            PermissionExpr.Arrow arrow => EvaluateArrow(arrow, resource, subject, inProgress),
            PermissionExpr.Union union => Combine(
                EvaluateExpr(union.Left, resource, subject, inProgress),
                EvaluateExpr(union.Right, resource, subject, inProgress),
                static (left, right) => left is CheckResult.Permit || right is CheckResult.Permit),
            PermissionExpr.Intersection intersection => Combine(
                EvaluateExpr(intersection.Left, resource, subject, inProgress),
                EvaluateExpr(intersection.Right, resource, subject, inProgress),
                static (left, right) => left is CheckResult.Permit && right is CheckResult.Permit),
            PermissionExpr.Exclusion exclusion => Combine(
                EvaluateExpr(exclusion.Left, resource, subject, inProgress),
                EvaluateExpr(exclusion.Right, resource, subject, inProgress),
                static (left, right) => left is CheckResult.Permit && right is CheckResult.Deny),
            _ => CheckResult.Deny,
        };

    private CheckResult EvaluateArrow(
        PermissionExpr.Arrow arrow,
        ObjectRef resource,
        SubjectRef subject,
        HashSet<(string Namespace, string Id, string Member)> inProgress)
    {
        foreach (var tuple in tuples.Read(resource, arrow.RelationName))
        {
            var parent = tuple.Subject.AsObjectRef();
            if (CheckMember(parent, arrow.Target, subject, inProgress) is CheckResult.Permit)
            {
                return CheckResult.Permit;
            }
        }

        return CheckResult.Deny;
    }

    private static CheckResult Combine(CheckResult left, CheckResult right, Func<CheckResult, CheckResult, bool> combine) =>
        combine(left, right) ? CheckResult.Permit : CheckResult.Deny;
}
