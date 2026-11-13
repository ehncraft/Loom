using Loom.Core;
using Loom.Evaluator;

using Microsoft.AspNetCore.Authorization;

namespace Loom.AspNetCore;

public sealed class LoomAuthorizationHandler(LoomEvaluator evaluator, IClaimsPrincipalMapper mapper)
    : AuthorizationHandler<LoomRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, LoomRequirement requirement)
    {
        var subject = mapper.Map(context.User);
        var resource = context.Resource switch
        {
            ObjectRef objectRef => objectRef,
            string resourceId => new ObjectRef(requirement.ResourceNamespace, resourceId),
            _ => ObjectRef.KindLevel(requirement.ResourceNamespace),
        };

        var result = await evaluator.CheckAsync(resource, requirement.Permission, subject);
        if (result is CheckResult.Permit)
        {
            context.Succeed(requirement);
        }
    }
}
