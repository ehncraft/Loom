using Loom.Core;

namespace Loom.Relationships;

public interface IRelationTupleWriter
{
    Task WriteAsync(RelationTuple tuple, CancellationToken cancellationToken);

    Task DeleteAsync(RelationTuple tuple, CancellationToken cancellationToken);
}
