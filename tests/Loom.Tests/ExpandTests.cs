using Loom.Core;
using Loom.Schema;
using Loom.Evaluator;
using Loom.Relationships;

namespace Loom.Tests;

public class ExpandTests
{
    [Fact]
    public async Task Expand_UnionPermission_ReturnsBothBranches()
    {
        var schema = SchemaParser.Parse(
            """
            definition user {}

            definition document {
                relation owner: user
                relation editor: user
                permission edit = owner + editor
            }
            """);

        var tuples = new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice")),
            new RelationTuple(new ObjectRef("document", "1"), "editor", new SubjectRef("user", "bob")),
        ]);

        var evaluator = new LoomEvaluator(schema, tuples);

        var tree = await evaluator.ExpandAsync(new ObjectRef("document", "1"), "edit");

        var union = Assert.IsType<ExpandNode.Union>(tree);
        Assert.Equal(2, union.Children.Count);

        var ownerLeaf = Assert.IsType<ExpandNode.Leaf>(union.Children[0]);
        Assert.Equal([new SubjectRef("user", "alice")], ownerLeaf.Subjects);

        var editorLeaf = Assert.IsType<ExpandNode.Leaf>(union.Children[1]);
        Assert.Equal([new SubjectRef("user", "bob")], editorLeaf.Subjects);
    }

    [Fact]
    public async Task Expand_NestedUserset_IncludesGroupSubtree()
    {
        var schema = SchemaParser.Parse(
            """
            definition user {}

            definition group {
                relation member: user
            }

            definition document {
                relation viewer: user | group#member
                permission view = viewer
            }
            """);

        var tuples = new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("document", "1"), "viewer", new SubjectRef("group", "eng", "member")),
            new RelationTuple(new ObjectRef("group", "eng"), "member", new SubjectRef("user", "dave")),
        ]);

        var evaluator = new LoomEvaluator(schema, tuples);

        var tree = await evaluator.ExpandAsync(new ObjectRef("document", "1"), "view");

        var union = Assert.IsType<ExpandNode.Union>(tree);
        var groupLeaf = Assert.IsType<ExpandNode.Leaf>(union.Children[1]);
        Assert.Equal([new SubjectRef("user", "dave")], groupLeaf.Subjects);
    }
}
