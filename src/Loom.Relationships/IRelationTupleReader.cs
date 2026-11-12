using Loom.Core;

namespace Loom.Relationships;

public interface IRelationTupleReader
{
    Task<IReadOnlyList<RelationTuple>> ReadAsync(ObjectRef resource, string relation, CancellationToken cancellationToken);

    Task<IReadOnlyList<ObjectRef>> ResourcesOfNamespaceAsync(string namespaceName, CancellationToken cancellationToken);

    Task<IReadOnlyList<SubjectRef>> SubjectsOfNamespaceAsync(string namespaceName, CancellationToken cancellationToken);
}
