using System.Text.Json;

namespace Loom.Core;

public static class JsonValueConverter
{
    public static object? ToClrValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var integer) ? (object)integer : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => element.EnumerateArray().Select(ToClrValue).ToList(),
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => ToClrValue(p.Value), StringComparer.Ordinal),
        _ => null,
    };

    public static Dictionary<string, object?>? ToClrValues(IReadOnlyDictionary<string, JsonElement>? values)
    {
        if (values is null)
        {
            return null;
        }

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, element) in values)
        {
            result[key] = ToClrValue(element);
        }

        return result;
    }
}
