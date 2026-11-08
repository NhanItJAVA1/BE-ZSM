using Microsoft.AspNetCore.Authorization;

namespace BE_ZSM.Authorization;

public sealed record PermissionRequirement(string Code) : IAuthorizationRequirement;
