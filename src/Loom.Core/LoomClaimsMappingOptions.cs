using System.Security.Claims;

namespace Loom.Core;

public sealed class LoomClaimsMappingOptions
{
    public string SubjectNamespace { get; set; } = "user";

    public string PrincipalIdClaimType { get; set; } = ClaimTypes.NameIdentifier;
}
