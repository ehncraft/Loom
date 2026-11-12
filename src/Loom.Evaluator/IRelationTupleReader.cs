using Loom.Core;

namespace Loom.Evaluator;

public interface IRelationTupleReader
{
    IEnumerable<RelationTuple> Read(ObjectRef resource, string relation);
}
