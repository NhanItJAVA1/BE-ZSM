using BE_ZSM.DTOs.GameModes;
using BE_ZSM.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BE_ZSM.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameModesController : ControllerBase
{
    private readonly IGameModeService _gameModeService;

    public GameModesController(
        IGameModeService gameModeService)
    {
        _gameModeService = gameModeService;
    }

    [HttpGet]
    public async Task<IActionResult> GetGameModes()
    {
        var gameModes =
            await _gameModeService.GetGameModesAsync();

        return Ok(gameModes);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetGameMode(int id)
    {
        var gameMode =
            await _gameModeService.GetGameModeAsync(id);

        return Ok(gameMode);
    }

    [HttpPost]
    public async Task<IActionResult> CreateGameMode(
        [FromBody] CreateGameModeDto dto)
    {
        var gameMode =
            await _gameModeService.CreateGameModeAsync(dto);

        return CreatedAtAction(
            nameof(GetGameMode),
            new { id = gameMode.Id },
            gameMode);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateGameMode(
        int id,
        [FromBody] UpdateGameModeDto dto)
    {
        var gameMode =
            await _gameModeService.UpdateGameModeAsync(
                id,
                dto);

        return Ok(gameMode);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGameMode(int id)
    {
        await _gameModeService.DeleteGameModeAsync(id);

        return NoContent();
    }
}