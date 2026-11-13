using System.Security.Claims;

using Loom.AspNetCore;
using Loom.Core;
using Loom.Schema;
using Loom.Evaluator;
using Loom.Relationships;

using Microsoft.AspNetCore.Authorization;

namespace Loom.Tests;

public class LoomAuthorizationHandlerTests
{
    private static readonly LoomSchema Schema = SchemaParser.Parse(
        """
        definition user {}

        definition document {
            relation owner: user
            permission edit = owner
        }
        """);

    private static ClaimsPrincipal UserPrincipal(string id) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "test"));

    private static LoomAuthorizationHandler CreateHandler(LoomEvaluator evaluator) =>
        new(evaluator, new ClaimsPrincipalMapper(new LoomClaimsMappingOptions()));

    [Fact]
    public async Task HandleRequirementAsync_SpecificResourceInstance_SucceedsWhenPermitted()
    {
        var evaluator = new LoomEvaluator(
            Schema,
            new InMemoryRelationTupleStore([
                new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice")),
            ]));

        var handler = CreateHandler(evaluator);
        var requirement = new LoomRequirement("document", "edit");
        var context = new AuthorizationHandlerContext(
            [requirement], UserPrincipal("alice"), new ObjectRef("document", "1"));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_SpecificResourceInstance_FailsWhenDenied()
    {
        var evaluator = new LoomEvaluator(
            Schema,
            new InMemoryRelationTupleStore([
                new RelationTuple(new ObjectRef("document", "1"), "owner", new SubjectRef("user", "alice")),
            ]));

        var handler = CreateHandler(evaluator);
        var requirement = new LoomRequirement("document", "edit");
        var context = new AuthorizationHandlerContext([requirement], UserPrincipal("bob"), new ObjectRef("document", "1"));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_StringResource_ResolvesIdAgainstRequirementNamespace()
    {
        var evaluator = new LoomEvaluator(
            Schema,
            new InMemoryRelationTupleStore([
                new RelationTuple(new ObjectRef("document", "42"), "owner", new SubjectRef("user", "alice")),
            ]));

        var handler = CreateHandler(evaluator);
        var requirement = new LoomRequirement("document", "edit");
        var context = new AuthorizationHandlerContext([requirement], UserPrincipal("alice"), "42");

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_NoResource_FallsBackToKindLevelSingleton()
    {
        var evaluator = new LoomEvaluator(
            Schema,
            new InMemoryRelationTupleStore([
                new RelationTuple(ObjectRef.KindLevel("document"), "owner", new SubjectRef("user", "alice")),
            ]));

        var handler = CreateHandler(evaluator);
        var requirement = new LoomRequirement("document", "edit");
        var context = new AuthorizationHandlerContext([requirement], UserPrincipal("alice"), resource: null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }
}
