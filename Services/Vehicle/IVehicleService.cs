using BE_ZSM.DTOs.Vehicles;

namespace BE_ZSM.Services.Vehicle
{
    public interface IVehicleService
    {
        Task<List<VehicleResponseDto>> GetVehiclesAsync();

        Task<VehicleResponseDto> GetVehicleAsync(int id);

        Task CreateVehicleAsync(CreateVehicleDto dto);

        Task UpdateVehicleAsync(
            int id,
            UpdateVehicleDto dto);

        Task DeleteVehicleAsync(int id);
    }
}
