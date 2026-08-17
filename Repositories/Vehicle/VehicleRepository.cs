using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Vehicles;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using BE_ZSM.Services;
using Microsoft.EntityFrameworkCore;
using VehicleEntity = BE_ZSM.Entities.Vehicle;

namespace BE_ZSM.Repositories.Vehicle
{
    public class VehicleRepository : IVehicleRepository
    {
        private readonly AppDbContext _context;
        private readonly DbSaveHelper _dbSaveHelper;
        public VehicleRepository(AppDbContext context, DbSaveHelper dbSaveHelper)
        {
            _context = context;
            _dbSaveHelper = dbSaveHelper;
        }
        public async Task AddAsync(VehicleEntity vehicle)
        {
            await _context.Vehicles.AddAsync(vehicle);
        }

        public void Delete(VehicleEntity vehicle)
        {
            _context.Vehicles.Remove(vehicle);
        }

        public async Task<List<VehicleResponseDto>> GetAllAsync()
        {
            return await _context.Vehicles
                .Select(v => new VehicleResponseDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    Type = v.Type,
                    Rank = v.Rank,
                    ImageUrl = v.ImageUrl,
                    CreatedAt = v.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<VehicleResponseDto?> GetByIdAsync(int id)
        {
            return await _context.Vehicles
                .Where(v => v.Id == id)
                .Select(v => new VehicleResponseDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    Type = v.Type,
                    Rank = v.Rank,
                    ImageUrl = v.ImageUrl,
                    CreatedAt = v.CreatedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<VehicleEntity>> GetByIdsAsync(List<int> vehicleIds)
        {
            return await _context.Vehicles
                .Where(v => vehicleIds.Contains(v.Id))
                .ToListAsync();
        }

        public async Task<VehicleEntity?> GetEntityByIdAsync(int id)
        {
            return await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task SaveChangesAsync()
        {
            await _dbSaveHelper.SaveChangesAsync();
        }
    }
}
