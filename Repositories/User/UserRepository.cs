using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using BE_ZSM.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;
    private readonly DbSaveHelper _dbSaveHelper;

    public UserRepository(
        AppDbContext context,
        DbSaveHelper dbSaveHelper)
    {
        _context = context;
        _dbSaveHelper = dbSaveHelper;
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _context.Users
            .Include(u => u.Role)
            .ToListAsync();
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        return await _context.Users
            .AnyAsync(u => u.Username == username);
    }

    public async Task<bool> ExistsByEmailAsync(
        string email,
        int? excludeUserId = null)
    {
        return await _context.Users
            .AnyAsync(u =>
                u.Email == email &&
                (!excludeUserId.HasValue || u.Id != excludeUserId.Value));
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public void Delete(User user)
    {
        _context.Users.Remove(user);
    }

    public async Task SaveChangesAsync()
    {
        await _dbSaveHelper.SaveChangesAsync();
    }
}