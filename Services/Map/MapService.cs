using AutoMapper;
using BE_ZSM.DTOs.Maps;
using BE_ZSM.Entities;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.UnitOfWork;
using BE_ZSM.Services.Interfaces;

namespace BE_ZSM.Services;

public class MapService : IMapService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly S3PresignedUrlService _presignedUrlService;
    private readonly IMapper _mapper;

    public MapService(
        IUnitOfWork unitOfWork,
        S3PresignedUrlService presignedUrlService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _presignedUrlService = presignedUrlService;
        _mapper = mapper;
    }

    public async Task<List<MapResponseDto>> GetMapsAsync()
    {
        var maps = await _unitOfWork.Maps.GetAllAsync();

        var responses = _mapper.Map<List<MapResponseDto>>(maps);

        foreach (var response in responses)
        {
            response.ImageUrl = _presignedUrlService.CreateGetUrlFromStoredUrl(response.ImageUrl);
        }

        return responses;
    }

    public async Task<MapResponseDto> GetMapAsync(int id)
    {
        var map = await _unitOfWork.Maps.GetByIdAsync(id);

        if (map == null)
        {
            throw new NotFoundException(
                "Map not found",
                "MAP_NOT_FOUND");
        }

        return _mapper.Map<MapResponseDto>(map);
    }

    public async Task<MapResponseDto> CreateMapAsync(
        CreateMapDto dto)
    {
        var map = _mapper.Map<Map>(dto);
        map.CreatedAt = DateTime.UtcNow;

        await _unitOfWork.Maps.AddAsync(map);

        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<MapResponseDto>(map);
    }

    public async Task<MapResponseDto> UpdateMapAsync(int id, UpdateMapDto dto)
    {
        var map = await _unitOfWork.Maps.GetByIdAsync(id);

        if (map == null)
        {
            throw new NotFoundException(
                "Map not found",
                "MAP_NOT_FOUND");
        }

        _mapper.Map(dto, map);
        map.CreatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<MapResponseDto>(map);
    }

    public async Task DeleteMapAsync(int id)
    {
        var map = await _unitOfWork.Maps.GetByIdAsync(id);

        if (map == null)
        {
            throw new NotFoundException(
                "Map not found",
                "MAP_NOT_FOUND");
        }

        _unitOfWork.Maps.Delete(map);

        await _unitOfWork.SaveChangesAsync();
    }
}