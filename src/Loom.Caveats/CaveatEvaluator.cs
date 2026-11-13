using System.Globalization;

namespace Loom.Caveats;

public static class CaveatEvaluator
{
    public static CaveatEvalResult Evaluate(CaveatDefinition definition, IReadOnlyDictionary<string, object?> context)
    {
        var missing = definition.Parameters
            .Where(p => !context.ContainsKey(p.Name))
            .Select(p => p.Name)
            .ToList();

        if (missing.Count > 0)
        {
            return new CaveatEvalResult.MissingContext(missing);
        }

        var value = EvaluateExpr(definition.Body, context);
        return new CaveatEvalResult.Satisfied((bool)value!);
    }

    private static object? EvaluateExpr(CelExpr expr, IReadOnlyDictionary<string, object?> context) => expr switch
    {
        CelExpr.Literal literal => literal.Value,
        CelExpr.Variable variable => context[variable.Name],
        CelExpr.LogicalNot negation => !(bool)EvaluateExpr(negation.Operand, context)!,
        CelExpr.LogicalAnd conjunction =>
            (bool)EvaluateExpr(conjunction.Left, context)! && (bool)EvaluateExpr(conjunction.Right, context)!,
        CelExpr.LogicalOr disjunction =>
            (bool)EvaluateExpr(disjunction.Left, context)! || (bool)EvaluateExpr(disjunction.Right, context)!,
        CelExpr.Comparison comparison => EvaluateComparison(comparison, context),
        CelExpr.InList inList => EvaluateInList(inList, context),
        _ => throw new InvalidOperationException($"Unsupported CEL expression node: {expr.GetType().Name}"),
    };

    private static bool EvaluateComparison(CelExpr.Comparison comparison, IReadOnlyDictionary<string, object?> context)
    {
        var left = EvaluateExpr(comparison.Left, context);
        var right = EvaluateExpr(comparison.Right, context);

        return comparison.Operator switch
        {
            ComparisonOperator.Equal => ValuesEqual(left, right),
            ComparisonOperator.NotEqual => !ValuesEqual(left, right),
            ComparisonOperator.LessThan => Compare(left, right) < 0,
            ComparisonOperator.LessOrEqual => Compare(left, right) <= 0,
            ComparisonOperator.GreaterThan => Compare(left, right) > 0,
            ComparisonOperator.GreaterOrEqual => Compare(left, right) >= 0,
            _ => throw new InvalidOperationException($"Unsupported comparison operator: {comparison.Operator}"),
        };
    }

    private static int Compare(object? left, object? right)
    {
        if (IsNumeric(left) && IsNumeric(right))
        {
            return Convert.ToDouble(left, CultureInfo.InvariantCulture)
                .CompareTo(Convert.ToDouble(right, CultureInfo.InvariantCulture));
        }

        if (left is IComparable comparable)
        {
            return comparable.CompareTo(right);
        }

        throw new InvalidOperationException($"Cannot order-compare value of type '{left?.GetType().Name ?? "null"}'.");
    }

    private static bool EvaluateInList(CelExpr.InList inList, IReadOnlyDictionary<string, object?> context)
    {
        var item = EvaluateExpr(inList.Item, context);
        var list = EvaluateExpr(inList.List, context);

        if (list is not System.Collections.IEnumerable enumerable)
        {
            throw new InvalidOperationException("The right-hand side of 'in' must be a list.");
        }

        foreach (var candidate in enumerable)
        {
            if (ValuesEqual(candidate, item))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (IsNumeric(left) && IsNumeric(right))
        {
            return Convert.ToDouble(left, CultureInfo.InvariantCulture)
                .Equals(Convert.ToDouble(right, CultureInfo.InvariantCulture));
        }

        return Equals(left, right);
    }

    private static bool IsNumeric(object? value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
