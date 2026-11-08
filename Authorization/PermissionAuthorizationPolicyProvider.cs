using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BE_ZSM.Authorization;

public sealed class PermissionAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider;

    public PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallbackProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallbackProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
            return _fallbackProvider.GetPolicyAsync(policyName);

        var permissionCode = policyName[HasPermissionAttribute.PolicyPrefix.Length..];
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissionCode))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
