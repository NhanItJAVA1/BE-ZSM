using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Enums;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Repositories.Role;

public class RoleRepository
    : GenericRepository<Entities.Role>,
      IRoleRepository
{
    public RoleRepository(AppDbContext context)
        : base(context)
    {
    }

    public async Task<Entities.Role?> GetByNameAsync(UserRole role)
    {
        return await _dbSet
            .FirstOrDefaultAsync(r => r.Name == role);
    }
}