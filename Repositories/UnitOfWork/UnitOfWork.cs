using BE_ZSM.Contexts;
using BE_ZSM.Repositories.Interfaces;
using BE_ZSM.Repositories.RefreshToken;
using BE_ZSM.Repositories.Role;
using BE_ZSM.Repositories.Vehicle;

namespace BE_ZSM.Repositories.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        public IUserRepository Users { get; }

        public IRoleRepository Roles { get; }

        public IRefreshTokenRepository RefreshTokens { get; }

        public IVehicleRepository Vehicles { get; }

        public IRecordRepository Records { get; }

        public IMapRepository Maps { get; }

        public IGameModeRepository GameModes { get; }
        public UnitOfWork(AppDbContext context, IUserRepository users, IRoleRepository roles, IRefreshTokenRepository refreshTokens, IVehicleRepository vehicles, IRecordRepository records, IMapRepository maps, IGameModeRepository gameModes)
        {
            _context = context;
            Users = users;
            Roles = roles;
            RefreshTokens = refreshTokens;
            Vehicles = vehicles;
            Records = records;
            Maps = maps;
            GameModes = gameModes;
        }
        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
