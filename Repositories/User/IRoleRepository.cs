using BE_ZSM.Entities;
using BE_ZSM.Enums;

namespace BE_ZSM.Repositories.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(UserRole role);
}