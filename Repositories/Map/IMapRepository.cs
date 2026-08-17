using BE_ZSM.Entities;

namespace BE_ZSM.Repositories.Interfaces;

using MapEntity = BE_ZSM.Entities.Map;

public interface IMapRepository
{
    Task<List<MapEntity>> GetAllAsync();

    Task<MapEntity?> GetByIdAsync(int id);

    Task AddAsync(MapEntity map);

    void Delete(MapEntity map);

    Task SaveChangesAsync();
}