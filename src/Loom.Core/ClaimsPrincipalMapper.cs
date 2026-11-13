using System.Security.Claims;

namespace Loom.Core;

public sealed class ClaimsPrincipalMapper(LoomClaimsMappingOptions options) : IClaimsPrincipalMapper
{
    public SubjectRef Map(ClaimsPrincipal claimsPrincipal)
    {
        var id = claimsPrincipal.FindFirst(options.PrincipalIdClaimType)?.Value
            ?? throw new InvalidOperationException(
                $"ClaimsPrincipal has no '{options.PrincipalIdClaimType}' claim to use as the subject id.");

        return new SubjectRef(options.SubjectNamespace, id);
    }
}
