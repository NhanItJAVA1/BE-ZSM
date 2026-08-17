using BE_ZSM.DTOs.Maps;
using BE_ZSM.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_ZSM.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MapsController : ControllerBase
{
    private readonly IMapService _mapService;

    public MapsController(IMapService mapService)
    {
        _mapService = mapService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMaps()
    {
        var maps = await _mapService.GetMapsAsync();

        return Ok(maps);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMap(int id)
    {
        var map = await _mapService.GetMapAsync(id);

        return Ok(map);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateMap(
        [FromBody] CreateMapDto dto)
    {
        var map = await _mapService.CreateMapAsync(dto);

        return CreatedAtAction(
            nameof(GetMap),
            new { id = map.Id },
            map);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMap(
        int id,
        [FromBody] UpdateMapDto dto)
    {
        var map = await _mapService.UpdateMapAsync(
            id,
            dto);

        return Ok(map);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMap(int id)
    {
        await _mapService.DeleteMapAsync(id);

        return NoContent();
    }
}