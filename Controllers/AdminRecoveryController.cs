using System.Globalization;
using System.Security.Claims;
using BE_ZSM.DTOs.AdminRecovery;
using BE_ZSM.Services.AdminRecovery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_ZSM.Controllers;

[ApiController]
[Authorize]
[Route("api/admin-recovery")]
public sealed class AdminRecoveryController(IAdminRecoveryService recoveryService) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> SearchUsers([FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!TryGetActorId(out var actorId))
            return Forbid();

        return Ok(await recoveryService.SearchUsersAsync(actorId, search, page, pageSize, cancellationToken));
    }

    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] RestoreAdminAccessDto request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId))
            return Forbid();

        await recoveryService.RestoreAsync(actorId, request.UserId, request.Reason, cancellationToken);
        return NoContent();
    }

    private bool TryGetActorId(out int actorId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out actorId)
        && actorId > 0;
}
