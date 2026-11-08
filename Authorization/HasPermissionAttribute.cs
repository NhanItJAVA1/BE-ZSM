using Microsoft.AspNetCore.Authorization;

namespace BE_ZSM.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "permission:";

    public HasPermissionAttribute(string permissionCode)
    {
        Policy = $"{PolicyPrefix}{permissionCode}";
    }
}
