using BE_ZSM.Entities;
using BE_ZSM.Repositories.Generic;
using RefreshTokenEntity = BE_ZSM.Entities.RefreshToken;
namespace BE_ZSM.Repositories.RefreshToken;

public interface IRefreshTokenRepository : IGenericRepository<RefreshTokenEntity>
{
    Task<RefreshTokenEntity?> GetByTokenAsync(string token);

}