using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Repositories.RefreshToken;

public class RefreshTokenRepository
    : GenericRepository<Entities.RefreshToken>,
      IRefreshTokenRepository
{
    public RefreshTokenRepository(AppDbContext context)
        : base(context)
    {
    }

    public async Task<Entities.RefreshToken?> GetByTokenAsync(string token)
    {
        return await _dbSet
            .Include(rt => rt.User)
            .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(rt => rt.Token == token);
    }
}