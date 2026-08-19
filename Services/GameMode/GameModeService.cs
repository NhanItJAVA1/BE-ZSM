using AutoMapper;
using BE_ZSM.DTOs.GameModes;
using BE_ZSM.Entities;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Interfaces;
using BE_ZSM.Repositories.UnitOfWork;
using BE_ZSM.Services.Interfaces;

namespace BE_ZSM.Services;

public class GameModeService : IGameModeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GameModeService(
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<List<GameModeResponseDto>> GetGameModesAsync()
    {
        var gameModes = await _unitOfWork.GameModes.GetAllAsync();

        return _mapper.Map<List<GameModeResponseDto>>(gameModes);
    }

    public async Task<GameModeResponseDto> GetGameModeAsync(int id)
    {
        var gameMode = await _unitOfWork.GameModes.GetByIdAsync(id);

        if (gameMode == null)
        {
            throw new NotFoundException(
                "Game mode not found",
                "GAME_MODE_NOT_FOUND");
        }

        return _mapper.Map<GameModeResponseDto>(gameMode);
    }

    public async Task<GameModeResponseDto> CreateGameModeAsync(CreateGameModeDto dto)
    {
        var gameMode = new GameMode
        {
            Name = dto.Name,
            Description = dto.Description
        };

        await _unitOfWork.GameModes.AddAsync(gameMode);

        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<GameModeResponseDto>(gameMode);
    }

    public async Task<GameModeResponseDto> UpdateGameModeAsync(int id, UpdateGameModeDto dto)
    {
        var gameMode = await _unitOfWork.GameModes.GetByIdAsync(id);

        if (gameMode == null)
        {
            throw new NotFoundException(
                "Game mode not found",
                "GAME_MODE_NOT_FOUND");
        }

        _mapper.Map(dto, gameMode);
        await _unitOfWork.SaveChangesAsync();
        return _mapper.Map<GameModeResponseDto>(gameMode);
    }

    public async Task DeleteGameModeAsync(int id)
    {
        var gameMode =
            await _unitOfWork.GameModes.GetByIdAsync(id);

        if (gameMode == null)
        {
            throw new NotFoundException(
                "Game mode not found",
                "GAME_MODE_NOT_FOUND");
        }

        _unitOfWork.GameModes.Delete(gameMode);

        await _unitOfWork.SaveChangesAsync();
    }   
}