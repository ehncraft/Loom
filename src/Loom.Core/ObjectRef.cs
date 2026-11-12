namespace Loom.Core;

public readonly record struct ObjectRef(string Namespace, string Id)
{
    public override string ToString() => $"{Namespace}:{Id}";
}
