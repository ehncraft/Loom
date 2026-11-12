using Loom.Core;
using Loom.Relationships;

namespace Loom.Tests;

public class RelationTupleStoreTests
{
    [Fact]
    public void Write_ThenRead_ReturnsTuple()
    {
        var store = new InMemoryRelationTupleStore();
        var tuple = new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice"));

        store.Write(tuple);

        Assert.Equal([tuple], store.Read(new ObjectRef("document", "1"), "owner"));
    }

    [Fact]
    public void Delete_RemovesTuple()
    {
        var tuple = new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice"));
        var store = new InMemoryRelationTupleStore([tuple]);

        store.Delete(tuple);

        Assert.Empty(store.Read(new ObjectRef("document", "1"), "owner"));
    }

    [Fact]
    public void ResourcesOfNamespace_ReturnsDistinctResources()
    {
        var store = new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice")),
            new RelationTuple(new ObjectRef("document", "1"), "editor", new SubjectRef("user", "bob")),
            new RelationTuple(new ObjectRef("document", "2"), "owner", new SubjectRef("user", "alice")),
        ]);

        var resources = store.ResourcesOfNamespace("document");

        Assert.Equal(
            [new ObjectRef("document", "1"), new ObjectRef("document", "2")],
            resources.OrderBy(r => r.Id));
    }

    [Fact]
    public void SubjectsOfNamespace_ExcludesUsersetReferences()
    {
        var store = new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("document", "1"), "viewer", new SubjectRef("user", "alice")),
            new RelationTuple(new ObjectRef("document", "1"), "viewer", new SubjectRef("group", "eng", "member")),
        ]);

        var subjects = store.SubjectsOfNamespace("user");

        Assert.Equal([new SubjectRef("user", "alice")], subjects);
    }
}
