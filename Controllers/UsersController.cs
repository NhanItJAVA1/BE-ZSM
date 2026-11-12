using BE_ZSM.Authorization;
using BE_ZSM.DTOs.Users;
using BE_ZSM.Services.Interfaces;
using BE_ZSM.Services.Rbac;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_ZSM.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IRbacManagementService _rbacService;

    public UsersController(IUserService userService, IRbacManagementService rbacService)
    {
        _userService = userService;
        _rbacService = rbacService;
    }

    [HttpGet]
    [HasPermission(PermissionCodes.UsersRead)]
    public async Task<IActionResult> GetUsers()
    {
        return Ok(await _userService.GetUsersAsync());
    }

    [HttpGet("{id}")]
    [HasPermission(PermissionCodes.UsersRead)]
    public async Task<IActionResult> GetUser(int id)
    {
        return Ok(await _userService.GetUserAsync(id));
    }

    [HttpPut("{id}")]
    [HasPermission(PermissionCodes.UsersUpdate)]
    public async Task<IActionResult> UpdateUser(
        int id,
        UpdateUserDto dto)
    {
        await _userService.UpdateAsync(id, dto);
        return Ok();
    }

    [HttpDelete("{id}")]
    [HasPermission(PermissionCodes.UsersDelete)]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/roles")]
    [HasPermission(PermissionCodes.UsersManageRoles)]
    public async Task<IActionResult> GetUserRoles(int id, CancellationToken cancellationToken)
    {
        return Ok(await _rbacService.GetUserRolesAsync(id, cancellationToken));
    }

    [HttpPut("{id:int}/roles")]
    [HasPermission(PermissionCodes.UsersManageRoles)]
    public async Task<IActionResult> ReplaceUserRoles(
        int id,
        [FromBody] BE_ZSM.DTOs.Rbac.ReplaceUserRolesDto dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _rbacService.ReplaceUserRolesAsync(id, dto, cancellationToken));
    }

}

