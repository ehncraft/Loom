namespace Loom.Schema;

public sealed class LoomSchema(IReadOnlyDictionary<string, ObjectTypeDefinition> types)
{
    public IReadOnlyDictionary<string, ObjectTypeDefinition> Types { get; } = types;

    public bool TryGetType(string name, out ObjectTypeDefinition? type) =>
        Types.TryGetValue(name, out type);
}
