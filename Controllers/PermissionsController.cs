using BE_ZSM.Authorization;
using BE_ZSM.Services.Rbac;
using Microsoft.AspNetCore.Mvc;

namespace BE_ZSM.Controllers;

[ApiController]
[Route("api/permissions")]
public sealed class PermissionsController : ControllerBase
{
    private readonly IRbacManagementService _rbacService;

    public PermissionsController(IRbacManagementService rbacService) => _rbacService = rbacService;

    [HttpGet]
    [HasPermission(PermissionCodes.PermissionsRead)]
    public async Task<IActionResult> GetPermissions(CancellationToken cancellationToken) =>
        Ok(await _rbacService.GetPermissionsAsync(cancellationToken));
}
