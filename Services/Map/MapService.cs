using BE_ZSM.DTOs.Maps;
using BE_ZSM.Entities;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories;
using BE_ZSM.Repositories.Interfaces;
using BE_ZSM.Services.Interfaces;

namespace BE_ZSM.Services;

public class MapService : IMapService
{
    private readonly IMapRepository _mapRepository;
    private readonly S3PresignedUrlService _presignedUrlService;

    public MapService(
        IMapRepository mapRepository,
        S3PresignedUrlService presignedUrlService)
    {
        _mapRepository = mapRepository;
        _presignedUrlService = presignedUrlService;
    }

    public async Task<List<MapResponseDto>> GetMapsAsync()
    {
        var maps = await _mapRepository.GetAllAsync();

        return maps
            .Select(MapToResponse)
            .ToList();
    }

    public async Task<MapResponseDto> GetMapAsync(int id)
    {
        var map = await _mapRepository.GetByIdAsync(id);

        if (map == null)
        {
            throw new NotFoundException(
                "Map not found",
                "MAP_NOT_FOUND");
        }

        return MapToResponse(map);
    }

    public async Task<MapResponseDto> CreateMapAsync(
        CreateMapDto dto)
    {
        var map = new Map
        {
            Name = dto.Name,
            Rate = dto.Rate,
            ImageUrl = dto.ImageUrl,
            CreatedAt = DateTime.UtcNow
        };

        await _mapRepository.AddAsync(map);

        await _mapRepository.SaveChangesAsync();

        return MapToResponse(map);
    }

    public async Task<MapResponseDto> UpdateMapAsync(
        int id,
        UpdateMapDto dto)
    {
        var map = await _mapRepository.GetByIdAsync(id);

        if (map == null)
        {
            throw new NotFoundException(
                "Map not found",
                "MAP_NOT_FOUND");
        }

        map.Name = dto.Name;
        map.Rate = dto.Rate;
        map.ImageUrl = dto.ImageUrl;

        await _mapRepository.SaveChangesAsync();

        return MapToResponse(map);
    }

    public async Task DeleteMapAsync(int id)
    {
        var map = await _mapRepository.GetByIdAsync(id);

        if (map == null)
        {
            throw new NotFoundException(
                "Map not found",
                "MAP_NOT_FOUND");
        }

        _mapRepository.Delete(map);

        await _mapRepository.SaveChangesAsync();
    }

    private MapResponseDto MapToResponse(Map map)
    {
        return new MapResponseDto
        {
            Id = map.Id,
            Name = map.Name,
            Rate = map.Rate,
            ImageUrl = _presignedUrlService
                .CreateGetUrlFromStoredUrl(map.ImageUrl),
            CreatedAt = map.CreatedAt
        };
    }
}