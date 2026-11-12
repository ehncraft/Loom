using Loom.Core;

namespace Loom.Relationships;

public interface IRelationTupleWriter
{
    void Write(RelationTuple tuple);

    void Delete(RelationTuple tuple);
}
