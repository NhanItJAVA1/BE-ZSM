using BE_ZSM.Services;
using VehicleEntity = BE_ZSM.Entities.Vehicle;

namespace BE_ZSM.Repositories.Vehicle
{
    public interface IVehicleRepository
        : IGenericRepository<VehicleEntity>
    {
        Task<List<VehicleEntity>> GetByIdsAsync(
            List<int> vehicleIds);
    }
}