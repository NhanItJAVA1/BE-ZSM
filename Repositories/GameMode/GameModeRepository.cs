using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using BE_ZSM.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Repositories;

public class GameModeRepository : IGameModeRepository
{
    private readonly AppDbContext _context;
    private readonly DbSaveHelper _dbSaveHelper;

    public GameModeRepository(
        AppDbContext context,
        DbSaveHelper dbSaveHelper)
    {
        _context = context;
        _dbSaveHelper = dbSaveHelper;
    }

    public async Task<List<GameMode>> GetAllAsync()
    {
        return await _context.GameModes
            .ToListAsync();
    }

    public async Task<GameMode?> GetByIdAsync(int id)
    {
        return await _context.GameModes
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task AddAsync(GameMode gameMode)
    {
        await _context.GameModes.AddAsync(gameMode);
    }

    public void Delete(GameMode gameMode)
    {
        _context.GameModes.Remove(gameMode);
    }

    public async Task SaveChangesAsync()
    {
        await _dbSaveHelper.SaveChangesAsync();
    }
}