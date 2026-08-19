using AutoMapper;
using BE_ZSM.DTOs.Maps;
using BE_ZSM.DTOs.Vehicles;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.UnitOfWork;
using BE_ZSM.Repositories.Vehicle;
using BE_ZSM.Services.Cache;

namespace BE_ZSM.Services.Vehicle
{
    public class VehicleService : IVehicleService
    {
        private readonly IVehicleRepository _vehicleRepository;
        private readonly S3PresignedUrlService _presignedUrlService;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private const string CacheKey = "maps:all";
        private readonly ICacheService _cache;

        public VehicleService(
            ICacheService cache,
        IVehicleRepository vehicleRepository,
        S3PresignedUrlService presignedUrlService,
        IMapper mapper,
        IUnitOfWork unitOfWork)
        {
            _cache = cache;
            _vehicleRepository = vehicleRepository;
            _presignedUrlService = presignedUrlService;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public async Task<VehicleResponseDto> CreateVehicleAsync(CreateVehicleDto dto)
        {
            var vehicle = _mapper.Map<Entities.Vehicle>(dto);
            vehicle.CreatedAt = DateTime.UtcNow;

            await _vehicleRepository.AddAsync(vehicle);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<VehicleResponseDto>(vehicle);
        }

        public async Task DeleteVehicleAsync(int id)
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(id);

            if (vehicle == null)
            {
                throw new NotFoundException(
                    "Vehicle not found",
                    "VEHICLE_NOT_FOUND");
            }

            _vehicleRepository.Delete(vehicle);

            await _unitOfWork.SaveChangesAsync();
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
            var response = _mapper.Map<VehicleResponseDto>(vehicle);

            response.ImageUrl =
                _presignedUrlService.CreateGetUrlFromStoredUrl(
                    response.ImageUrl);

            return response;
        }

        public async Task<List<VehicleResponseDto>> GetVehiclesAsync()
        {
            var cached = await _cache.GetAsync<List<VehicleResponseDto>>(CacheKey);

            if (cached != null)
            {
                return cached;
            }
            var vehicles = await _vehicleRepository.GetAllAsync();
            var responses = _mapper.Map<List<VehicleResponseDto>>(vehicles);

            responses.ForEach(x => x.ImageUrl = _presignedUrlService.CreateGetUrlFromStoredUrl(x.ImageUrl));

            await _cache.SetAsync(CacheKey, responses, TimeSpan.FromMinutes(30));

            return responses;
        }

        public async Task<VehicleResponseDto> UpdateVehicleAsync(int id, UpdateVehicleDto dto)
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(id);

            if (vehicle == null)
            {
                throw new NotFoundException(
                    "Vehicle not found",
                    "VEHICLE_NOT_FOUND");
            }

            _mapper.Map(dto, vehicle);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<VehicleResponseDto>(vehicle);
        }
    }
}
    