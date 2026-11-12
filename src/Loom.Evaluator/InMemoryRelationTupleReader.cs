using Loom.Core;

namespace Loom.Evaluator;

public sealed class InMemoryRelationTupleReader : IRelationTupleReader
{
    private readonly ILookup<(string Namespace, string Id, string Relation), RelationTuple> _index;

    public InMemoryRelationTupleReader(IEnumerable<RelationTuple> tuples)
    {
        _index = tuples.ToLookup(t => (t.Resource.Namespace, t.Resource.Id, t.Relation));
    }

    public IEnumerable<RelationTuple> Read(ObjectRef resource, string relation) =>
        _index[(resource.Namespace, resource.Id, relation)];
}
