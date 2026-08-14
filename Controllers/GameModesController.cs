using BE_ZSM.Contexts;
using BE_ZSM.DTOs.GameModes;
using BE_ZSM.Entities;
using BE_ZSM.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameModesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly DbSaveHelper _dbSaveHelper;

        public GameModesController(AppDbContext context, DbSaveHelper dbSaveHelper)
        {
            _context = context;
            _dbSaveHelper = dbSaveHelper;
        }

        [HttpGet]
        public async Task<IActionResult> GetGameModes()
        {
            try
            {
                var gameModes = await _context.GameModes
                    .Select(g => new
                    {
                        g.Id,
                        g.Name,
                        g.Description
                    })
                    .ToListAsync();

                return Ok(gameModes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetGameMode(int id)
        {
            var gameMode = await _context.GameModes
                .Where(g => g.Id == id)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.Description
                })
                .FirstOrDefaultAsync();

            if (gameMode == null)
            {
                return NotFound(new
                {
                    message = "Game mode not found"
                });
            }

            return Ok(gameMode);
        }

        [HttpPost]
        public async Task<IActionResult> CreateGameMode(
            CreateGameModeDto dto)
        {
            var gameMode = new GameMode
            {
                Name = dto.Name,
                Description = dto.Description
            };

            _context.GameModes.Add(gameMode);

            var saveError = await _dbSaveHelper.TrySaveChangesAsync();
            if (saveError != null)
            {
                return BadRequest(new
                {
                    message = saveError
                });
            }

            return CreatedAtAction(
                nameof(GetGameMode),
                new { id = gameMode.Id },
                gameMode
            );
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGameMode(
            int id,
            UpdateGameModeDto dto)
        {
            var gameMode = await _context.GameModes
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gameMode == null)
            {
                return NotFound(new
                {
                    message = "Game mode not found"
                });
            }

            gameMode.Name = dto.Name;
            gameMode.Description = dto.Description;

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
                gameMode.Id,
                gameMode.Name,
                gameMode.Description
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGameMode(int id)
        {
            var gameMode = await _context.GameModes
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gameMode == null)
            {
                return NotFound(new
                {
                    message = "Game mode not found"
                });
            }

            _context.GameModes.Remove(gameMode);

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