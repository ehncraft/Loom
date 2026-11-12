using Loom.Core;
using Loom.Relationships;

namespace Loom.Tests;

public class RelationTupleStoreTests
{
    [Fact]
    public async Task Write_ThenRead_ReturnsTuple()
    {
        var store = new InMemoryRelationTupleStore();
        var tuple = new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice"));

        await store.WriteAsync(tuple, CancellationToken.None);

        Assert.Equal([tuple], await store.ReadAsync(new ObjectRef("document", "1"), "owner", CancellationToken.None));
    }

    [Fact]
    public async Task Delete_RemovesTuple()
    {
        var tuple = new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice"));
        var store = new InMemoryRelationTupleStore([tuple]);

        await store.DeleteAsync(tuple, CancellationToken.None);

        Assert.Empty(await store.ReadAsync(new ObjectRef("document", "1"), "owner", CancellationToken.None));
    }

    [Fact]
    public async Task ResourcesOfNamespace_ReturnsDistinctResources()
    {
        var store = new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice")),
            new RelationTuple(new ObjectRef("document", "1"), "editor", new SubjectRef("user", "bob")),
            new RelationTuple(new ObjectRef("document", "2"), "owner", new SubjectRef("user", "alice")),
        ]);

        var resources = await store.ResourcesOfNamespaceAsync("document", CancellationToken.None);

        Assert.Equal(
            [new ObjectRef("document", "1"), new ObjectRef("document", "2")],
            resources.OrderBy(r => r.Id));
    }

    [Fact]
    public async Task SubjectsOfNamespace_ExcludesUsersetReferences()
    {
        var store = new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("document", "1"), "viewer", new SubjectRef("user", "alice")),
            new RelationTuple(new ObjectRef("document", "1"), "viewer", new SubjectRef("group", "eng", "member")),
        ]);

        var subjects = await store.SubjectsOfNamespaceAsync("user", CancellationToken.None);

        Assert.Equal([new SubjectRef("user", "alice")], subjects);
    }
}
