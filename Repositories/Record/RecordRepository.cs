using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using BE_ZSM.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Repositories;

public class RecordRepository : GenericRepository<Record>, IRecordRepository
{
    public RecordRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<Record>> GetAllApprovedAsync()
    {
        return await _dbSet
            .Where(r => r.Status == Enums.RecordStatus.Approved)
            .Include(r => r.User)
            .Include(r => r.Map)
            .Include(r => r.GameMode)
            .Include(r => r.Vehicle)
            .ToListAsync();
    }

    public async Task<Record?> GetByIdWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(r => r.User)
            .Include(r => r.Map)
            .Include(r => r.GameMode)
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<Record>> GetByUserIdAsync(int userId)
    {
        return await _dbSet
            .Where(r => r.UserId == userId)
            .Include(r => r.User)
            .Include(r => r.Map)
            .Include(r => r.GameMode)
            .Include(r => r.Vehicle)
            .ToListAsync();
    }

    public async Task<List<Record>> GetPendingAsync()
    {
        return await _dbSet
            .Where(r => r.Status == Enums.RecordStatus.Pending)
            .Include(r => r.User)
            .Include(r => r.Map)
            .Include(r => r.GameMode)
            .Include(r => r.Vehicle)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<Record?> GetEntityByIdAsync(int id)
    {
        return await _dbSet
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<Record>> GetApprovedByMapIdAsync(int mapId)
    {
        return await _dbSet
            .Where(r =>
                r.MapId == mapId &&
                r.Status == Enums.RecordStatus.Approved)
            .ToListAsync();
    }
}