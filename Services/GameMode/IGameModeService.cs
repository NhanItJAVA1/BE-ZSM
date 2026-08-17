using BE_ZSM.DTOs.GameModes;

namespace BE_ZSM.Services.Interfaces;

public interface IGameModeService
{
    Task<List<GameModeResponseDto>> GetGameModesAsync();

    Task<GameModeResponseDto> GetGameModeAsync(int id);

    Task<GameModeResponseDto> CreateGameModeAsync(
        CreateGameModeDto dto);

    Task<GameModeResponseDto> UpdateGameModeAsync(
        int id,
        UpdateGameModeDto dto);

    Task DeleteGameModeAsync(int id);
}