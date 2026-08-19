using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Repositories;
public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context)
    { 
    }
    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _dbSet
             .Include(u => u.Role)
             .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        return await _dbSet
            .AnyAsync(u => u.Username == username);
    }

    public async Task<bool> ExistsByEmailAsync(
        string email,
        int? excludeUserId = null)
    {
        return await _dbSet
             .AnyAsync(u =>
                 u.Email == email &&
                 (!excludeUserId.HasValue || u.Id != excludeUserId.Value));
    }

    public async Task<User?> GetByIdWithRoleAsync(int id)
    {
        return await _dbSet
             .Include(u => u.Role)
             .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<List<User>> GetAllWithRoleAsync()
    {
        return await _dbSet
            .Include(u => u.Role)
            .ToListAsync();
    }
}