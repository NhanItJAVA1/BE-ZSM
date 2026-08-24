using AutoMapper;
using BE_ZSM.DTOs.Maps;
using BE_ZSM.Entities;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Generic;
using BE_ZSM.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Services;

public class MapService : IMapService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly S3PresignedUrlService _presignedUrlService;
    private readonly IGenericRepository<Map> _mapRepo;
    private readonly IMapper _mapper;

    public MapService(        IUnitOfWork unitOfWork,
        S3PresignedUrlService presignedUrlService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _presignedUrlService = presignedUrlService;
        _mapRepo = _unitOfWork.GetRepository<Map>();
        _mapper = mapper;
    }

    public async Task<List<MapResponseDto>> GetMapsAsync()
    {
        var maps = await _mapRepo.All().AsNoTracking().ToListAsync();

        var responses = _mapper.Map<List<MapResponseDto>>(maps);

        foreach (var response in responses)
        {
            response.ImageUrl = await _presignedUrlService.CreateGetUrlFromStoredUrl(response.ImageUrl);
        }
        
        return responses;
    }

    public async Task<MapResponseDto> GetMapAsync(int id)
    {
        var map = await _mapRepo.FindAsync(m => m.Id == id);

        if (map == null)
        {
            throw new NotFoundException(
                "Map not found",
                "MAP_NOT_FOUND");
        }

        return _mapper.Map<MapResponseDto>(map);
    }

    public async Task CreateMapAsync(CreateMapDto dto)
    {
        var map = _mapper.Map<Map>(dto);
        map.CreatedAt = DateTime.UtcNow;

        await _mapRepo.CreateAsync(map);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UpdateMapAsync(int id, UpdateMapDto dto)
    {
        var map = await _mapRepo.FindAsync(m => m.Id == id);

        if (map == null)
        {
            throw new NotFoundException(
                "Map not found",
                "MAP_NOT_FOUND");
        }

        _mapper.Map(dto, map);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteMapAsync(int id)
    {
        var map = await _mapRepo.FindAsync(m => m.Id == id);

        if (map == null)
        {
            throw new NotFoundException(
                "Map not found",
                "MAP_NOT_FOUND");
        }

        await _mapRepo.DeleteAsync(map);
        await _unitOfWork.SaveChangesAsync();
    }
}