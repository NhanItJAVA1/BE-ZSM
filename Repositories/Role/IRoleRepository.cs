using BE_ZSM.Enums;
using BE_ZSM.Services;
using RoleEntity = BE_ZSM.Entities.Role;

namespace BE_ZSM.Repositories.Role;

public interface IRoleRepository : IGenericRepository<RoleEntity>
{
    Task<RoleEntity?> GetByNameAsync(UserRole role);
}