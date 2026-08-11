using BE_ZSM.Contexts;
using BE_ZSM.DTOs.Maps;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
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

        public MapsController(AppDbContext context, DbSaveHelper dbSaveHelper)
        {
            _context = context;
            _dbSaveHelper = dbSaveHelper;
        }

        // GET: api/Maps
        [HttpGet]
        public async Task<IActionResult> GetMaps()
        {
            var maps = await _context.Maps
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Slug,
                    m.ImageUrl,
                    m.CreatedAt
                })
                .ToListAsync();

            return Ok(maps);
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
                    m.Slug,
                    m.ImageUrl,
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
        public async Task<IActionResult> CreateMap(CreateMapDto dto)
        {
            var map = new Map
            {
                Name = dto.Name,
                Slug = dto.Slug,
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
            map.Slug = dto.Slug;
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
                map.Slug,
                map.ImageUrl,
                map.CreatedAt
            });
        }

        // DELETE: api/Maps/{id}
        [HttpDelete("{id}")]
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