using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using BE_ZSM.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using MapEntity = BE_ZSM.Entities.Map;

namespace BE_ZSM.Repositories;

public class MapRepository : IMapRepository
{
    private readonly AppDbContext _context;
    private readonly DbSaveHelper _dbSaveHelper;

    public MapRepository(
        AppDbContext context,
        DbSaveHelper dbSaveHelper)
    {
        _context = context;
        _dbSaveHelper = dbSaveHelper;
    }

    public async Task<List<MapEntity>> GetAllAsync()
    {
        return await _context.Maps
            .ToListAsync();
    }

    public async Task<MapEntity?> GetByIdAsync(int id)
    {
        return await _context.Maps
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task AddAsync(MapEntity map)
    {
        await _context.Maps.AddAsync(map);
    }

    public void Delete(MapEntity map)
    {
        _context.Maps.Remove(map);
    }

    public async Task SaveChangesAsync()
    {
        await _dbSaveHelper.SaveChangesAsync();
    }
}