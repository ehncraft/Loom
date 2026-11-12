using Loom.Core;

namespace Loom.Relationships;

public interface IRelationTupleReader
{
    IEnumerable<RelationTuple> Read(ObjectRef resource, string relation);

    IEnumerable<ObjectRef> ResourcesOfNamespace(string namespaceName);

    IEnumerable<SubjectRef> SubjectsOfNamespace(string namespaceName);
}
