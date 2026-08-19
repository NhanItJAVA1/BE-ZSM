using BE_ZSM.Contexts;
using Microsoft.EntityFrameworkCore;

using VehicleEntity = BE_ZSM.Entities.Vehicle;

namespace BE_ZSM.Repositories.Vehicle
{
    public class VehicleRepository
        : GenericRepository<VehicleEntity>,
          IVehicleRepository
    {
        public VehicleRepository(AppDbContext context)
            : base(context)
        {
        }

        public async Task<List<VehicleEntity>> GetByIdsAsync(
            List<int> vehicleIds)
        {
            return await _dbSet
                .Where(v => vehicleIds.Contains(v.Id))
                .ToListAsync();
        }
    }
}