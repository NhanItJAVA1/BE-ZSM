using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Enums;
using BE_ZSM.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _context;

    public RoleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Role?> GetByNameAsync(UserRole role)
    {
        return await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == role);
    }
}