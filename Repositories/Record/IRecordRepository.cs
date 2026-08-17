using BE_ZSM.Entities;
using RecordEntity = BE_ZSM.Entities.Record;

namespace BE_ZSM.Repositories.Interfaces;

public interface IRecordRepository
{
    Task<List<RecordEntity>> GetAllApprovedAsync();

    Task<RecordEntity?> GetByIdAsync(int id);

    Task<List<RecordEntity>> GetByUserIdAsync(int userId);

    Task<List<RecordEntity>> GetPendingAsync();

    Task<RecordEntity?> GetEntityByIdAsync(int id);

    Task AddAsync(RecordEntity record);

    void Delete(RecordEntity record);

    Task<List<RecordEntity>> GetApprovedByMapIdAsync(int mapId);

    Task SaveChangesAsync();
}