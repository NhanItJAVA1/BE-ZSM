using BE_ZSM.Entities;
using BE_ZSM.Services;
using RecordEntity = BE_ZSM.Entities.Record;

namespace BE_ZSM.Repositories.Interfaces;

public interface IRecordRepository : IGenericRepository<RecordEntity>
{
    Task<List<RecordEntity>> GetAllApprovedAsync();
    Task<RecordEntity?> GetByIdWithDetailsAsync(int id);
    Task<List<RecordEntity>> GetByUserIdAsync(int userId);
    Task<List<RecordEntity>> GetPendingAsync();
    Task<RecordEntity?> GetEntityByIdAsync(int id);
    Task<List<RecordEntity>> GetApprovedByMapIdAsync(int mapId);
}