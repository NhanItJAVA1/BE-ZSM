using BE_ZSM.DTOs.Rbac;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BE_ZSM.Services.Rbac;

public interface IRbacManagementService
{
    Task<IReadOnlyList<RoleResponseDto>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<RoleResponseDto> CreateRoleAsync(SaveRoleDto dto, CancellationToken cancellationToken = default);
    Task<RoleResponseDto> UpdateRoleAsync(int id, SaveRoleDto dto, CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionResponseDto>> GetPermissionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionResponseDto>> GetRolePermissionsAsync(int roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionResponseDto>> ReplaceRolePermissionsAsync(int roleId, ReplaceRolePermissionsDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleResponseDto>> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleResponseDto>> ReplaceUserRolesAsync(int userId, ReplaceUserRolesDto dto, CancellationToken cancellationToken = default);
}
