using BE_ZSM.Contexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace BE_ZSM.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PermissionAuthorizationHandler(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!PermissionCodes.All.Contains(requirement.Code) || context.User.Identity?.IsAuthenticated != true
            || !int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            || userId <= 0)
        {
            return;
        }

        var cancellationToken = _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var hasPermission = await _dbContext.Users
             .AsNoTracking()
             .AnyAsync(user => user.Id == userId && !user.IsSystemOwner && user.UserRoles
             .Any(userRole => userRole.Role.RolePermissions
             .Any(rolePermission => rolePermission.Permission.Code == requirement.Code)), cancellationToken);

        if (hasPermission)
            context.Succeed(requirement);
    }

}
