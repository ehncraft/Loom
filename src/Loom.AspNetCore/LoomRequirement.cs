using Microsoft.AspNetCore.Authorization;

namespace Loom.AspNetCore;

public sealed record LoomRequirement(string ResourceNamespace, string Permission) : IAuthorizationRequirement;
