namespace Loom.Schema;

public sealed record RelationDefinition(string Name, IReadOnlyList<RelationTypeRef> AllowedTypes);
