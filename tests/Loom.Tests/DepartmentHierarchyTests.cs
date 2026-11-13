using Loom.Core;
using Loom.Schema;
using Loom.Evaluator;
using Loom.Relationships;

namespace Loom.Tests;

public class DepartmentHierarchyTests
{
    private static LoomEvaluator CreateEvaluator()
    {
        var schema = SchemaParser.Parse(
            """
            definition membership {}

            definition tenant {
                relation admin: membership
            }

            definition department {
                relation tenant: tenant
                relation lead: membership
                relation staff: membership
                permission approve_requests = lead + tenant->admin
                permission view_requests = approve_requests + staff
            }
            """);

        var tuples = new InMemoryRelationTupleStore([
            new RelationTuple(new ObjectRef("tenant", "acme"), "admin", new SubjectRef("membership", "root")),
            new RelationTuple(new ObjectRef("department", "finance"), "tenant", new SubjectRef("tenant", "acme")),
            new RelationTuple(new ObjectRef("department", "finance"), "lead", new SubjectRef("membership", "carol")),
            new RelationTuple(new ObjectRef("department", "finance"), "staff", new SubjectRef("membership", "dave")),
        ]);

        return new LoomEvaluator(schema, tuples);
    }

    [Fact]
    public async Task ApproveRequests_DepartmentLead_Permits()
    {
        var evaluator = CreateEvaluator();

        var result = await evaluator.CheckAsync(
            new ObjectRef("department", "finance"), "approve_requests", new SubjectRef("membership", "carol"));

        Assert.IsType<CheckResult.Permit>(result);
    }

    [Fact]
    public async Task ApproveRequests_DepartmentStaffWithoutLead_Denies()
    {
        var evaluator = CreateEvaluator();

        var result = await evaluator.CheckAsync(
            new ObjectRef("department", "finance"), "approve_requests", new SubjectRef("membership", "dave"));

        Assert.IsType<CheckResult.Deny>(result);
    }

    [Fact]
    public async Task ApproveRequests_TenantAdmin_InheritsThroughDepartmentViaTupleToUserset()
    {
        var evaluator = CreateEvaluator();

        var result = await evaluator.CheckAsync(
            new ObjectRef("department", "finance"), "approve_requests", new SubjectRef("membership", "root"));

        Assert.IsType<CheckResult.Permit>(result);
    }

    [Fact]
    public async Task ViewRequests_DepartmentStaff_PermitsViaUnion()
    {
        var evaluator = CreateEvaluator();

        var result = await evaluator.CheckAsync(
            new ObjectRef("department", "finance"), "view_requests", new SubjectRef("membership", "dave"));

        Assert.IsType<CheckResult.Permit>(result);
    }

    [Fact]
    public async Task ApproveRequests_UnrelatedTenantAdmin_Denies()
    {
        var evaluator = CreateEvaluator();

        var result = await evaluator.CheckAsync(
            new ObjectRef("department", "finance"), "approve_requests", new SubjectRef("membership", "someone-else"));

        Assert.IsType<CheckResult.Deny>(result);
    }
}
