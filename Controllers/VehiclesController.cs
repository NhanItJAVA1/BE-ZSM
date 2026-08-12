using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Vehicles;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using BE_ZSM.Services;
using Microsoft.AspNetCore.Authorization;
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
        private readonly S3PresignedUrlService _presignedUrlService;

        public VehiclesController(AppDbContext context, DbSaveHelper dbSaveHelper, S3PresignedUrlService presignedUrlService)
        {
            _context = context;
            _dbSaveHelper = dbSaveHelper;
            _presignedUrlService = presignedUrlService;
        }

        // GET: api/Vehicles
        [HttpGet]
        public async Task<IActionResult> GetVehicles()
        {
            try
            {
                var vehicles = await _context.Vehicles
                    .Select(v => new
                    {
                        v.Id,
                        v.Name,
                        v.Rank,
                        v.Type,
                        ImageUrl = string.IsNullOrWhiteSpace(v.ImageUrl) ? null : _presignedUrlService.CreateGetUrl(
                            _presignedUrlService.GetObjectKeyFromUrl(v.ImageUrl)
                            ),
                        v.CreatedAt
                    })
                    .ToListAsync();

                return Ok(vehicles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
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
                    v.Rank,
                    v.Type,
                    ImageUrl = string.IsNullOrWhiteSpace(v.ImageUrl) ? null : _presignedUrlService.CreateGetUrl(
                            _presignedUrlService.GetObjectKeyFromUrl(v.ImageUrl)
                        ),
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateVehicle(
            CreateVehicleDto dto)
        {
            var vehicle = new Vehicle
            {
                Name = dto.Name,
                Rank = dto.Rank,
                Type = dto.Type,
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
        [Authorize(Roles = "Admin")]
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
            vehicle.Rank = dto.Rank;
            vehicle.Type = dto.Type;
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
                vehicle.Rank,
                vehicle.Type,
                vehicle.ImageUrl,
                vehicle.CreatedAt
            });
        }

        // DELETE: api/Vehicles/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
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