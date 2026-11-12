using Loom.Core;

namespace Loom.Relationships;

public sealed class InMemoryRelationTupleStore : IRelationTupleStore
{
    private readonly object _gate = new();
    private readonly HashSet<RelationTuple> _tuples = [];
    private readonly Dictionary<(string Namespace, string Id, string Relation), HashSet<RelationTuple>> _byResource = [];

    public InMemoryRelationTupleStore(IEnumerable<RelationTuple>? seed = null)
    {
        if (seed is null)
        {
            return;
        }

        foreach (var tuple in seed)
        {
            Write(tuple);
        }
    }

    public void Write(RelationTuple tuple)
    {
        lock (_gate)
        {
            if (!_tuples.Add(tuple))
            {
                return;
            }

            var key = (tuple.Resource.Namespace, tuple.Resource.Id, tuple.Relation);
            if (!_byResource.TryGetValue(key, out var set))
            {
                set = [];
                _byResource[key] = set;
            }

            set.Add(tuple);
        }
    }

    public void Delete(RelationTuple tuple)
    {
        lock (_gate)
        {
            if (!_tuples.Remove(tuple))
            {
                return;
            }

            var key = (tuple.Resource.Namespace, tuple.Resource.Id, tuple.Relation);
            if (_byResource.TryGetValue(key, out var set))
            {
                set.Remove(tuple);
            }
        }
    }

    public IEnumerable<RelationTuple> Read(ObjectRef resource, string relation)
    {
        lock (_gate)
        {
            var key = (resource.Namespace, resource.Id, relation);
            return _byResource.TryGetValue(key, out var set) ? [.. set] : [];
        }
    }

    public IEnumerable<ObjectRef> ResourcesOfNamespace(string namespaceName)
    {
        lock (_gate)
        {
            return _tuples
                .Where(t => t.Resource.Namespace == namespaceName)
                .Select(t => t.Resource)
                .Distinct()
                .ToList();
        }
    }

    public IEnumerable<SubjectRef> SubjectsOfNamespace(string namespaceName)
    {
        lock (_gate)
        {
            return _tuples
                .Where(t => t.Subject.Relation is null && t.Subject.Namespace == namespaceName)
                .Select(t => t.Subject)
                .Distinct()
                .ToList();
        }
    }
}
