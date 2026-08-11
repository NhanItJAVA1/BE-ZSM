using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Vehicles;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VehiclesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly DbSaveHelper _dbSaveHelper;

        public VehiclesController(AppDbContext context, DbSaveHelper dbSaveHelper)
        {
            _context = context;
            _dbSaveHelper = dbSaveHelper;
        }

        // GET: api/Vehicles
        [HttpGet]
        public async Task<IActionResult> GetVehicles()
        {
            var vehicles = await _context.Vehicles
                .Select(v => new
                {
                    v.Id,
                    v.Name,
                    v.Slug,
                    v.ImageUrl,
                    v.CreatedAt
                })
                .ToListAsync();

            return Ok(vehicles);
        }

        // GET: api/Vehicles/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetVehicle(int id)
        {
            var vehicle = await _context.Vehicles
                .Where(v => v.Id == id)
                .Select(v => new
                {
                    v.Id,
                    v.Name,
                    v.Slug,
                    v.ImageUrl,
                    v.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (vehicle == null)
            {
                return NotFound(new
                {
                    message = "Vehicle not found"
                });
            }

            return Ok(vehicle);
        }

        // POST: api/Vehicles
        [HttpPost]
        public async Task<IActionResult> CreateVehicle(
            CreateVehicleDto dto)
        {
            var vehicle = new Vehicle
            {
                Name = dto.Name,
                Slug = dto.Slug,
                ImageUrl = dto.ImageUrl,
                CreatedAt = DateTime.UtcNow
            };

            _context.Vehicles.Add(vehicle);

            var saveError = await _dbSaveHelper.TrySaveChangesAsync();
            if (saveError != null)
            {
                return BadRequest(new
                {
                    message = saveError
                });
            }

            return CreatedAtAction(
                nameof(GetVehicle),
                new { id = vehicle.Id },
                vehicle
            );
        }

        // PUT: api/Vehicles/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateVehicle(
            int id,
            UpdateVehicleDto dto)
        {
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vehicle == null)
            {
                return NotFound(new
                {
                    message = "Vehicle not found"
                });
            }

            vehicle.Name = dto.Name;
            vehicle.Slug = dto.Slug;
            vehicle.ImageUrl = dto.ImageUrl;

            var saveError = await _dbSaveHelper.TrySaveChangesAsync();
            if (saveError != null)
            {
                return BadRequest(new
                {
                    message = saveError
                });
            }

            return Ok(new
            {
                vehicle.Id,
                vehicle.Name,
                vehicle.Slug,
                vehicle.ImageUrl,
                vehicle.CreatedAt
            });
        }

        // DELETE: api/Vehicles/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVehicle(int id)
        {
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vehicle == null)
            {
                return NotFound(new
                {
                    message = "Vehicle not found"
                });
            }

            _context.Vehicles.Remove(vehicle);

            var saveError = await _dbSaveHelper.TrySaveChangesAsync();
            if (saveError != null)
            {
                return BadRequest(new
                {
                    message = saveError
                });
            }

            return NoContent();
        }
    }
}