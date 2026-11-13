using System.Security.Claims;

namespace Loom.Core;

public interface IClaimsPrincipalMapper
{
    SubjectRef Map(ClaimsPrincipal claimsPrincipal);
}
