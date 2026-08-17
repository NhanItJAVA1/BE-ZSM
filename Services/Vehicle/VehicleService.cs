using BE_ZSM.DTOs.Vehicles;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Vehicle;

namespace BE_ZSM.Services.Vehicle
{
    public class VehicleService : IVehicleService
    {
        private readonly IVehicleRepository _vehicleRepository;
        private readonly S3PresignedUrlService _presignedUrlService;

        public VehicleService(
        IVehicleRepository vehicleRepository,
        S3PresignedUrlService presignedUrlService)
        {
            _vehicleRepository = vehicleRepository;
            _presignedUrlService = presignedUrlService;
        }

        public async Task<VehicleResponseDto> CreateVehicleAsync(CreateVehicleDto dto)
        {
            var vehicle = new Entities.Vehicle
            {
                Name = dto.Name,
                Rank = dto.Rank,
                Type = dto.Type,
                ImageUrl = dto.ImageUrl,
                CreatedAt = DateTime.UtcNow
            };

            await _vehicleRepository.AddAsync(vehicle);
            await _vehicleRepository.SaveChangesAsync();

            return new VehicleResponseDto
            {
                Id = vehicle.Id,
                Name = vehicle.Name,
                Type = vehicle.Type,
                Rank = vehicle.Rank,
                ImageUrl = vehicle.ImageUrl,
                CreatedAt = vehicle.CreatedAt
            };
        }

        public async Task DeleteVehicleAsync(int id)
        {
            var vehicle =
           await _vehicleRepository.GetEntityByIdAsync(id);

            if (vehicle == null)
            {
                throw new NotFoundException(
                    "Vehicle not found",
                    "VEHICLE_NOT_FOUND");
            }

            _vehicleRepository.Delete(vehicle);

            await _vehicleRepository.SaveChangesAsync();
        }

        public async Task<VehicleResponseDto> GetVehicleAsync(int id)
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(id);

            if (vehicle == null)
            {
                throw new NotFoundException(
                    "Vehicle not found",
                    "VEHICLE_NOT_FOUND");
            }

            vehicle.ImageUrl =
                _presignedUrlService.CreateGetUrlFromStoredUrl(
                    vehicle.ImageUrl);

            return vehicle;
        }

        public async Task<List<VehicleResponseDto>> GetVehiclesAsync()
        {
            var vehicles = await _vehicleRepository.GetAllAsync();

            foreach (var vehicle in vehicles)
            {
                vehicle.ImageUrl =
                    _presignedUrlService.CreateGetUrlFromStoredUrl(
                        vehicle.ImageUrl);
            }

            return vehicles;
        }

        public async Task<VehicleResponseDto> UpdateVehicleAsync(int id, UpdateVehicleDto dto)
        {
            var vehicle = await _vehicleRepository.GetEntityByIdAsync(id);

            if (vehicle == null)
            {
                throw new NotFoundException(
                    "Vehicle not found",
                    "VEHICLE_NOT_FOUND");
            }

            vehicle.Name = dto.Name;
            vehicle.Rank = dto.Rank;
            vehicle.Type = dto.Type;
            vehicle.ImageUrl = dto.ImageUrl;

            await _vehicleRepository.SaveChangesAsync();

            return new VehicleResponseDto
            {
                Id = vehicle.Id,
                Name = vehicle.Name,
                Type = vehicle.Type,
                Rank = vehicle.Rank,
                ImageUrl = vehicle.ImageUrl,
                CreatedAt = vehicle.CreatedAt
            };
        }
    }
}
    