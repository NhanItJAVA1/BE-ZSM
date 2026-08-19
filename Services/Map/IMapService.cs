using BE_ZSM.DTOs.Maps;
using BE_ZSM.Entities;

namespace BE_ZSM.Services.Interfaces;

public interface IMapService
{
    Task<List<MapResponseDto>> GetMapsAsync();

    Task<MapResponseDto> GetMapAsync(int id);

    Task<MapResponseDto> CreateMapAsync(CreateMapDto dto);

    Task<MapResponseDto> UpdateMapAsync(
        int id,
        UpdateMapDto dto);

    Task DeleteMapAsync(int id);
}