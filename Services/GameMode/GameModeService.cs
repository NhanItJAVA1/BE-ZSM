using AutoMapper;
using BE_ZSM.DTOs.GameModes;
using BE_ZSM.Entities;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Generic;
using BE_ZSM.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Services;

public class GameModeService : IGameModeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IGenericRepository<GameMode> _gamemodeRepo;

    public GameModeService(
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _gamemodeRepo = _unitOfWork.GetRepository<GameMode>();
    }

    public async Task<List<GameModeResponseDto>> GetGameModesAsync()
    {
        var gameModes = await _gamemodeRepo
            .All()
            .AsNoTracking()
            .ToListAsync();

        return _mapper.Map<List<GameModeResponseDto>>(gameModes);
    }

    public async Task<GameModeResponseDto> GetGameModeAsync(int id)
    {
        var gameMode = await _gamemodeRepo.FindAsync(g => g.Id == id);

        if (gameMode == null)
        {
            throw new NotFoundException(
                "Game mode not found",
                "GAME_MODE_NOT_FOUND");
        }

        return _mapper.Map<GameModeResponseDto>(gameMode);
    }

    public async Task CreateGameModeAsync(CreateGameModeDto dto)
    {
        var gameMode = new GameMode
        {
            Name = dto.Name,
            Description = dto.Description
        };

        await _gamemodeRepo.CreateAsync(gameMode);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UpdateGameModeAsync(int id, UpdateGameModeDto dto)
    {
        var gameMode = await _gamemodeRepo.FindAsync(g => g.Id == id);

        if (gameMode == null)
        {
            throw new NotFoundException(
                "Game mode not found",
                "GAME_MODE_NOT_FOUND");
        }

        _mapper.Map(dto, gameMode);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteGameModeAsync(int id)
    {
        var gameMode =
            await _gamemodeRepo.FindAsync(g => g.Id == id);

        if (gameMode == null)
        {
            throw new NotFoundException(
                "Game mode not found",
                "GAME_MODE_NOT_FOUND");
        }

        await _gamemodeRepo.DeleteAsync(gameMode);

        await _unitOfWork.SaveChangesAsync();
    }   
}