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
        return Ok(await _gameModeService.GetGameModesAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetGameMode(int id)
    {
        return Ok(await _gameModeService.GetGameModeAsync(id));
    }

    [HttpPost]
    public async Task<IActionResult> CreateGameMode([FromBody] CreateGameModeDto dto)
    {
        await _gameModeService.CreateGameModeAsync(dto);

        return Ok();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateGameMode(
        int id,
        [FromBody] UpdateGameModeDto dto)
    {
        await _gameModeService.UpdateGameModeAsync(id, dto);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGameMode(int id)
    {
        await _gameModeService.DeleteGameModeAsync(id);
        return NoContent();
    }
}