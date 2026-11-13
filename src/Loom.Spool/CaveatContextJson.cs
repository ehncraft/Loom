using System.Text.Json;

using Loom.Core;

namespace Loom.Spool;

internal static class CaveatContextJson
{
    public static string? Serialize(IReadOnlyDictionary<string, object?>? context) =>
        context is null || context.Count == 0 ? null : JsonSerializer.Serialize(context);

    public static IReadOnlyDictionary<string, object?>? Deserialize(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        var elements = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            elements[property.Name] = property.Value;
        }

        return JsonValueConverter.ToClrValues(elements);
    }
}
