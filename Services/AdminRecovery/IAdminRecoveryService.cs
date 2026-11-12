using BE_ZSM.DTOs.AdminRecovery;
using BE_ZSM.Responses;

namespace BE_ZSM.Services.AdminRecovery;

public interface IAdminRecoveryService
{
    Task<PagedResult<AdminRecoveryUserDto>> SearchUsersAsync(int actorUserId, string? search, int page = 1, int pageSize = 20,
        CancellationToken cancellationToken = default);
    Task RestoreAsync(int actorUserId, int targetUserId, string reason, CancellationToken cancellationToken = default);
}
