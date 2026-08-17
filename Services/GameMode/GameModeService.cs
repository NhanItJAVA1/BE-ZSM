using BE_ZSM.DTOs.GameModes;
using BE_ZSM.Entities;
using BE_ZSM.Exceptions;
using BE_ZSM.Repositories.Interfaces;
using BE_ZSM.Services.Interfaces;

namespace BE_ZSM.Services;

public class GameModeService : IGameModeService
{
    private readonly IGameModeRepository _gameModeRepository;

    public GameModeService(
        IGameModeRepository gameModeRepository)
    {
        _gameModeRepository = gameModeRepository;
    }

    public async Task<List<GameModeResponseDto>> GetGameModesAsync()
    {
        var gameModes =
            await _gameModeRepository.GetAllAsync();

        return gameModes
            .Select(MapToResponse)
            .ToList();
    }

    public async Task<GameModeResponseDto> GetGameModeAsync(int id)
    {
        var gameMode =
            await _gameModeRepository.GetByIdAsync(id);

        if (gameMode == null)
        {
            throw new NotFoundException(
                "Game mode not found",
                "GAME_MODE_NOT_FOUND");
        }

        return MapToResponse(gameMode);
    }

    public async Task<GameModeResponseDto> CreateGameModeAsync(
        CreateGameModeDto dto)
    {
        var gameMode = new GameMode
        {
            Name = dto.Name,
            Description = dto.Description
        };

        await _gameModeRepository.AddAsync(gameMode);

        await _gameModeRepository.SaveChangesAsync();

        return MapToResponse(gameMode);
    }

    public async Task<GameModeResponseDto> UpdateGameModeAsync(
        int id,
        UpdateGameModeDto dto)
    {
        var gameMode =
            await _gameModeRepository.GetByIdAsync(id);

        if (gameMode == null)
        {
            throw new NotFoundException(
                "Game mode not found",
                "GAME_MODE_NOT_FOUND");
        }

        gameMode.Name = dto.Name;
        gameMode.Description = dto.Description;

        await _gameModeRepository.SaveChangesAsync();

        return MapToResponse(gameMode);
    }

    public async Task DeleteGameModeAsync(int id)
    {
        var gameMode =
            await _gameModeRepository.GetByIdAsync(id);

        if (gameMode == null)
        {
            throw new NotFoundException(
                "Game mode not found",
                "GAME_MODE_NOT_FOUND");
        }

        _gameModeRepository.Delete(gameMode);

        await _gameModeRepository.SaveChangesAsync();
    }

    private static GameModeResponseDto MapToResponse(
        GameMode gameMode)
    {
        return new GameModeResponseDto
        {
            Id = gameMode.Id,
            Name = gameMode.Name,
            Description = gameMode.Description
        };
    }
}