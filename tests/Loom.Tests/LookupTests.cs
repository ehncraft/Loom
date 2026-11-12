using Loom.Core;
using Loom.Schema;
using Loom.Evaluator;
using Loom.Relationships;

namespace Loom.Tests;

public class LookupTests
{
    private static LoomEvaluator CreateEvaluator(out SubjectRef alice, out SubjectRef bob)
    {
        var schema = SchemaParser.Parse(
            """
            definition user {}

            definition document {
                relation viewer: user
                permission view = viewer
            }
            """);

        alice = new SubjectRef("user", "alice");
        bob = new SubjectRef("user", "bob");

        var tuples = new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("document", "1"), "viewer", alice),
            new RelationTuple(new ObjectRef("document", "2"), "viewer", bob),
            new RelationTuple(new ObjectRef("document", "3"), "viewer", alice),
        ]);

        return new LoomEvaluator(schema, tuples);
    }

    [Fact]
    public async Task LookupResources_ReturnsOnlyResourcesSubjectCanAccess()
    {
        var evaluator = CreateEvaluator(out var alice, out _);

        var resources = (await evaluator.LookupResourcesAsync(alice, "view", "document")).OrderBy(r => r.Id);

        Assert.Equal([new ObjectRef("document", "1"), new ObjectRef("document", "3")], resources);
    }

    [Fact]
    public async Task LookupSubjects_ReturnsOnlySubjectsWithAccess()
    {
        var evaluator = CreateEvaluator(out var alice, out _);

        var subjects = await evaluator.LookupSubjectsAsync(new ObjectRef("document", "1"), "view", "user");

        Assert.Equal([alice], subjects);
    }
}
