using BE_ZSM.Repositories.Interfaces;
using BE_ZSM.Repositories.RefreshToken;
using BE_ZSM.Repositories.Role;
using BE_ZSM.Repositories.Vehicle;

namespace BE_ZSM.Repositories.UnitOfWork
{
    public interface IUnitOfWork
    {
        IUserRepository Users { get; }

        IRoleRepository Roles { get; }

        IRefreshTokenRepository RefreshTokens { get; }

        IVehicleRepository Vehicles { get; }

        IRecordRepository Records { get; }

        IMapRepository Maps { get; }

        IGameModeRepository GameModes { get; }

        Task<int> SaveChangesAsync();
    }
}
