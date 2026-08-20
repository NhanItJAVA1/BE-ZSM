using AutoMapper;
using BE_ZSM.DTOs.Vehicles;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Generic;
using BE_ZSM.Services.Cache;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Services.Vehicle
{
    public class VehicleService : IVehicleService
    {
        private readonly S3PresignedUrlService _presignedUrlService;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<Entities.Vehicle> _vehicleRepo;

        public VehicleService(
            ICacheService cache,
            S3PresignedUrlService presignedUrlService,
            IMapper mapper,
            IUnitOfWork unitOfWork)
        {
            _presignedUrlService = presignedUrlService;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _vehicleRepo = _unitOfWork.GetRepository<Entities.Vehicle>();
        }

        public async Task CreateVehicleAsync(CreateVehicleDto dto)
        {
            var vehicle = _mapper.Map<Entities.Vehicle>(dto);
            vehicle.CreatedAt = DateTime.UtcNow;

            var repository = _vehicleRepo;

            await repository.CreateAsync(vehicle);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteVehicleAsync(int id)
        {
            var repository = _vehicleRepo;

            var vehicle = await repository.FindAsync(v => v.Id == id);

            if (vehicle == null)
            {
                throw new NotFoundException(
                    "Vehicle not found",
                    "VEHICLE_NOT_FOUND");
            }

            await repository.DeleteAsync(vehicle);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<VehicleResponseDto> GetVehicleAsync(int id)
        {
            var repository = _vehicleRepo;
            var vehicle = await repository.FindAsync(v => v.Id == id);

            if (vehicle == null)
            {
                throw new NotFoundException(
                    "Vehicle not found",
                    "VEHICLE_NOT_FOUND");
            }
            var response = _mapper.Map<VehicleResponseDto>(vehicle);

            response.ImageUrl = await _presignedUrlService.CreateGetUrlFromStoredUrl(response.ImageUrl);

            return response;
        }

        public async Task<List<VehicleResponseDto>> GetVehiclesAsync()
        {
            var repository = _vehicleRepo;
            var vehicles = await repository.All().AsNoTracking().ToListAsync();
            var responses = _mapper.Map<List<VehicleResponseDto>>(vehicles);

            foreach (var response in responses)
            {
                response.ImageUrl = await _presignedUrlService.CreateGetUrlFromStoredUrl(response.ImageUrl);
            }

            return responses;
        }

        public async Task UpdateVehicleAsync(int id, UpdateVehicleDto dto)
        {
            var repository = _vehicleRepo;
            var vehicle = await repository.FindAsync(v => v.Id == id);

            if (vehicle == null)
            {
                throw new NotFoundException(
                    "Vehicle not found",
                    "VEHICLE_NOT_FOUND");
            }

            _mapper.Map(dto, vehicle);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
    