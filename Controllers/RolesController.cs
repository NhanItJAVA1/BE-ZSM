using BE_ZSM.Authorization;
using BE_ZSM.DTOs.Rbac;
using BE_ZSM.Services.Rbac;
using Microsoft.AspNetCore.Mvc;

namespace BE_ZSM.Controllers;

[ApiController]
[Route("api/roles")]
public sealed class RolesController : ControllerBase
{
    private readonly IRbacManagementService _rbacService;
    public RolesController(IRbacManagementService rbacService)
    {
        _rbacService = rbacService;
    }

    [HttpGet]
    [HasPermission(PermissionCodes.RolesRead)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken) =>
        Ok(await _rbacService.GetRolesAsync(cancellationToken));

    [HttpPost]
    [HasPermission(PermissionCodes.RolesCreate)]
    public async Task<IActionResult> CreateRole([FromBody] SaveRoleDto dto, CancellationToken cancellationToken)
    {
        var role = await _rbacService.CreateRoleAsync(dto, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, role);
    }

    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.RolesUpdate)]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] SaveRoleDto dto, CancellationToken cancellationToken)
    {
        return Ok(await _rbacService.UpdateRoleAsync(id, dto, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(PermissionCodes.RolesDelete)]
    public async Task<IActionResult> DeleteRole(int id, CancellationToken cancellationToken)
    {
        await _rbacService.DeleteRoleAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/permissions")]
    [HasPermission(PermissionCodes.RolesManagePermissions)]
    public async Task<IActionResult> GetRolePermissions(int id, CancellationToken cancellationToken) =>
        Ok(await _rbacService.GetRolePermissionsAsync(id, cancellationToken));

    [HttpPut("{id:int}/permissions")]
    [HasPermission(PermissionCodes.RolesManagePermissions)]
    public async Task<IActionResult> ReplaceRolePermissions(int id, [FromBody] ReplaceRolePermissionsDto dto, CancellationToken cancellationToken)
    {
        return Ok(await _rbacService.ReplaceRolePermissionsAsync(id, dto, cancellationToken));
    }
}
