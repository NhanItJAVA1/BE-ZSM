using BE_ZSM.Entities;
using GameModeEntity = BE_ZSM.Entities.GameMode;

namespace BE_ZSM.Repositories.Interfaces;

public interface IGameModeRepository
{
    Task<List<GameModeEntity>> GetAllAsync();

    Task<GameModeEntity?> GetByIdAsync(int id);

    Task AddAsync(GameModeEntity gameMode);

    void Delete(GameModeEntity gameMode);

    Task SaveChangesAsync();
}