namespace Loom.Schema;

public sealed class ObjectTypeDefinition(
    string name,
    IReadOnlyDictionary<string, RelationDefinition> relations,
    IReadOnlyDictionary<string, PermissionDefinition> permissions)
{
    public string Name { get; } = name;

    public IReadOnlyDictionary<string, RelationDefinition> Relations { get; } = relations;

    public IReadOnlyDictionary<string, PermissionDefinition> Permissions { get; } = permissions;
}
