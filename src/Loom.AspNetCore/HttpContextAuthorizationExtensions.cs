using Loom.Core;
using Loom.Evaluator;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.AspNetCore;

public static class HttpContextAuthorizationExtensions
{
    public static Task<CheckResult> CheckAsync(
        this LoomEvaluator evaluator,
        HttpContext httpContext,
        ObjectRef resource,
        string permission,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default)
    {
        var mapper = httpContext.RequestServices.GetRequiredService<IClaimsPrincipalMapper>();
        var subject = mapper.Map(httpContext.User);
        return evaluator.CheckAsync(resource, permission, subject, context, cancellationToken);
    }
}
