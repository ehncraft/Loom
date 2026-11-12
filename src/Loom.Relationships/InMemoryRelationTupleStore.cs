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
            WriteCore(tuple);
        }
    }

    public Task WriteAsync(RelationTuple tuple, CancellationToken cancellationToken)
    {
        WriteCore(tuple);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(RelationTuple tuple, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_tuples.Remove(tuple))
            {
                var key = (tuple.Resource.Namespace, tuple.Resource.Id, tuple.Relation);
                if (_byResource.TryGetValue(key, out var set))
                {
                    set.Remove(tuple);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RelationTuple>> ReadAsync(ObjectRef resource, string relation, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var key = (resource.Namespace, resource.Id, relation);
            IReadOnlyList<RelationTuple> result = _byResource.TryGetValue(key, out var set) ? [.. set] : [];
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<ObjectRef>> ResourcesOfNamespaceAsync(string namespaceName, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<ObjectRef> result = _tuples
                .Where(t => t.Resource.Namespace == namespaceName)
                .Select(t => t.Resource)
                .Distinct()
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<SubjectRef>> SubjectsOfNamespaceAsync(string namespaceName, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<SubjectRef> result = _tuples
                .Where(t => t.Subject.Relation is null && t.Subject.Namespace == namespaceName)
                .Select(t => t.Subject)
                .Distinct()
                .ToList();
            return Task.FromResult(result);
        }
    }

    private void WriteCore(RelationTuple tuple)
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
}
