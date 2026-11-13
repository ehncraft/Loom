using System.Text.Json;

using Loom.Core;

namespace Loom.Tests;

public class JsonValueConverterTests
{
    [Fact]
    public void ToClrValues_MixedTypes_ConvertsEachToItsNaturalClrType()
    {
        using var document = JsonDocument.Parse(
            """{"region":"eu-west-1","count":5,"active":true,"tags":["a","b"]}""");

        var elements = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            elements[property.Name] = property.Value;
        }

        var values = JsonValueConverter.ToClrValues(elements)!;

        Assert.Equal("eu-west-1", values["region"]);
        Assert.Equal(5L, (long)values["count"]!);
        Assert.True((bool)values["active"]!);
        var tags = Assert.IsType<List<object?>>(values["tags"]);
        Assert.Equal(["a", "b"], tags);
    }

    [Fact]
    public void ToClrValues_NullInput_ReturnsNull()
    {
        Assert.Null(JsonValueConverter.ToClrValues(null));
    }
}
