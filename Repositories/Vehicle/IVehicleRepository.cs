using BE_ZSM.DTOs.Vehicles;
using VehicleEntity = BE_ZSM.Entities.Vehicle;

namespace BE_ZSM.Repositories.Vehicle
{
    public interface IVehicleRepository
    {
        Task<List<VehicleResponseDto>> GetAllAsync();

        Task<VehicleResponseDto?> GetByIdAsync(int id);

        Task<VehicleEntity?> GetEntityByIdAsync(int id);

        Task<List<VehicleEntity>> GetByIdsAsync(List<int> vehicleIds);

        Task AddAsync(VehicleEntity vehicle);

        void Delete(VehicleEntity vehicle);

        Task SaveChangesAsync();
    }
}