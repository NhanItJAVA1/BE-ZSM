using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using BE_ZSM.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Repositories;

public class RecordRepository : IRecordRepository
{
    private readonly AppDbContext _context;
    private readonly DbSaveHelper _dbSaveHelper;

    public RecordRepository(
        AppDbContext context,
        DbSaveHelper dbSaveHelper)
    {
        _context = context;
        _dbSaveHelper = dbSaveHelper;
    }

    public async Task<List<Record>> GetAllApprovedAsync()
    {
        return await _context.Records
            .Where(r => r.Status == Enums.RecordStatus.Approved)
            .Include(r => r.User)
            .Include(r => r.Map)
            .Include(r => r.GameMode)
            .Include(r => r.Vehicle)
            .ToListAsync();
    }

    public async Task<Record?> GetByIdAsync(int id)
    {
        return await _context.Records
            .Include(r => r.User)
            .Include(r => r.Map)
            .Include(r => r.GameMode)
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<Record>> GetByUserIdAsync(int userId)
    {
        return await _context.Records
            .Where(r => r.UserId == userId)
            .Include(r => r.User)
            .Include(r => r.Map)
            .Include(r => r.GameMode)
            .Include(r => r.Vehicle)
            .ToListAsync();
    }

    public async Task<List<Record>> GetPendingAsync()
    {
        return await _context.Records
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
        return await _context.Records
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task AddAsync(Record record)
    {
        await _context.Records.AddAsync(record);
    }

    public void Delete(Record record)
    {
        _context.Records.Remove(record);
    }

    public async Task<List<Record>> GetApprovedByMapIdAsync(int mapId)
    {
        return await _context.Records
            .Where(r =>
                r.MapId == mapId &&
                r.Status == Enums.RecordStatus.Approved)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _dbSaveHelper.SaveChangesAsync();
    }
}