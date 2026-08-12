using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Maps;
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
    public class MapsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly DbSaveHelper _dbSaveHelper;
        private readonly S3PresignedUrlService _presignedUrlService;

        public MapsController(AppDbContext context, DbSaveHelper dbSaveHelper, S3PresignedUrlService presignedUrlService)
        {
            _context = context;
            _dbSaveHelper = dbSaveHelper;
            _presignedUrlService = presignedUrlService;
        }

        // GET: api/Maps
        [HttpGet]
        public async Task<IActionResult> GetMaps()
        {
            try
            {
                var maps = await _context.Maps               
                    .ToListAsync();

                var result = maps.Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Rate,
                    ImageUrl = string.IsNullOrWhiteSpace(m.ImageUrl)?null:_presignedUrlService.CreateGetUrl(
                        _presignedUrlService.GetObjectKeyFromUrl(m.ImageUrl)
                        ),
                    m.CreatedAt
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        // GET: api/Maps/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMap(int id)
        {
            var map = await _context.Maps
                .Where(m => m.Id == id)
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Rate,
                    ImageUrl = string.IsNullOrWhiteSpace(m.ImageUrl)?null:_presignedUrlService.CreateGetUrl(
                        _presignedUrlService.GetObjectKeyFromUrl(m.ImageUrl)
                        ),
                    m.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (map == null)
            {
                return NotFound(new
                {
                    message = "Map not found"
                });
            }

            return Ok(map);
        }

        // POST: api/Maps
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateMap(CreateMapDto dto)
        {
            var imageKey =  $"catalog/images/maps/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}.jpg";
            var map = new Map
            {
                Name = dto.Name,
                Rate = dto.Rate,
                ImageUrl = dto.ImageUrl,
                CreatedAt = DateTime.UtcNow
            };

            _context.Maps.Add(map);

            var saveError = await _dbSaveHelper.TrySaveChangesAsync();
            if (saveError != null)
            {
                return BadRequest(new
                {
                    message = saveError
                });
            }

            return CreatedAtAction(
                nameof(GetMap),
                new { id = map.Id },
                map
            );
        }

        // PUT: api/Maps/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateMap(
            int id,
            UpdateMapDto dto)
        {
            var map = await _context.Maps
                .FirstOrDefaultAsync(m => m.Id == id);

            if (map == null)
            {
                return NotFound(new
                {
                    message = "Map not found"
                });
            }

            map.Name = dto.Name;
            map.Rate = dto.Rate;
            map.ImageUrl = dto.ImageUrl;

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
                map.Id,
                map.Name,
                map.Rate,
                map.ImageUrl,
                map.CreatedAt
            });
        }

        // DELETE: api/Maps/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMap(int id)
        {
            var map = await _context.Maps
                .FirstOrDefaultAsync(m => m.Id == id);

            if (map == null)
            {
                return NotFound(new
                {
                    message = "Map not found"
                });
            }

            _context.Maps.Remove(map);

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