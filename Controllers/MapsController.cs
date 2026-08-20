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
        return Ok(await _mapService.GetMapsAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMap(int id)
    {
        return Ok(await _mapService.GetMapAsync(id));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateMap(
        [FromBody] CreateMapDto dto)
    {
        await _mapService.CreateMapAsync(dto);

        return Ok(new {message = "Map created successfully"});
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMap(
        int id,
        [FromBody] UpdateMapDto dto)
    {
        await _mapService.UpdateMapAsync(id, dto);

        return Ok();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMap(int id)
    {
        await _mapService.DeleteMapAsync(id);

        return NoContent();
    }
}