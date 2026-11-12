using Loom.Core;
using Loom.Schema;
using Loom.Evaluator;
using Loom.Relationships;

namespace Loom.Tests;

public class LoomEvaluatorTests
{
    [Fact]
    public async Task Check_DirectRelationGrantsAccess_Permits()
    {
        var schema = SchemaParser.Parse(
            """
            definition user {}

            definition document {
                relation owner: user
                permission edit = owner
            }
            """);

        var tuples = new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice")),
        ]);

        var evaluator = new LoomEvaluator(schema, tuples);

        var result = await evaluator.CheckAsync(new ObjectRef("document", "1"), "edit", new SubjectRef("user", "alice"));

        Assert.IsType<CheckResult.Permit>(result);
    }

    [Fact]
    public async Task Check_ComputedUsersetInheritance_EditorImpliesView()
    {
        var schema = SchemaParser.Parse(
            """
            definition user {}

            definition document {
                relation owner: user
                relation editor: user
                relation viewer: user
                permission edit = owner + editor
                permission view = edit + viewer
            }
            """);

        var tuples = new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("document", "1"), "editor", new SubjectRef("user", "bob")),
        ]);

        var evaluator = new LoomEvaluator(schema, tuples);

        var result = await evaluator.CheckAsync(new ObjectRef("document", "1"), "view", new SubjectRef("user", "bob"));

        Assert.IsType<CheckResult.Permit>(result);
    }

    [Fact]
    public async Task Check_TupleToUsersetHierarchy_FolderViewerSeesDocument()
    {
        var schema = SchemaParser.Parse(
            """
            definition user {}

            definition folder {
                relation viewer: user
                permission view = viewer
            }

            definition document {
                relation owner: user
                relation parent: folder
                permission edit = owner
                permission view = edit + parent->view
            }
            """);

        var tuples = new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("document", "1"), "parent", new SubjectRef("folder", "A")),
            new RelationTuple(new ObjectRef("folder", "A"), "viewer", new SubjectRef("user", "carol")),
        ]);

        var evaluator = new LoomEvaluator(schema, tuples);

        var result = await evaluator.CheckAsync(new ObjectRef("document", "1"), "view", new SubjectRef("user", "carol"));

        Assert.IsType<CheckResult.Permit>(result);
    }

    [Fact]
    public async Task Check_NestedUsersetViaGroupMembership_ResolvesTransitively()
    {
        var schema = SchemaParser.Parse(
            """
            definition user {}

            definition group {
                relation member: user | group#member
            }

            definition document {
                relation viewer: user | group#member
                permission view = viewer
            }
            """);

        var tuples = new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("group", "eng"), "member", new SubjectRef("user", "dave")),
            new RelationTuple(new ObjectRef("document", "1"), "viewer", new SubjectRef("group", "eng", "member")),
        ]);

        var evaluator = new LoomEvaluator(schema, tuples);

        var result = await evaluator.CheckAsync(new ObjectRef("document", "1"), "view", new SubjectRef("user", "dave"));

        Assert.IsType<CheckResult.Permit>(result);
    }

    [Fact]
    public async Task Check_CyclicPermissionDefinition_DeniesInsteadOfOverflowing()
    {
        var schema = SchemaParser.Parse(
            """
            definition cyclic {
                permission a = b
                permission b = a
            }
            """);

        var tuples = new InMemoryRelationTupleStore([]);
        var evaluator = new LoomEvaluator(schema, tuples);

        var result = await evaluator.CheckAsync(new ObjectRef("cyclic", "1"), "a", new SubjectRef("user", "anyone"));

        Assert.IsType<CheckResult.Deny>(result);
    }
}
