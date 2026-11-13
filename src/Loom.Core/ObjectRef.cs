namespace Loom.Core;

public readonly record struct ObjectRef(string Namespace, string Id)
{
    public const string KindLevelId = "_";

    public static ObjectRef KindLevel(string @namespace) => new(@namespace, KindLevelId);

    public override string ToString() => $"{Namespace}:{Id}";
}
