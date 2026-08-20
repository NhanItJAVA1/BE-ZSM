using BE_ZSM.DTOs.GameModes;

namespace BE_ZSM.Services.Interfaces;

public interface IGameModeService
{
    Task<List<GameModeResponseDto>> GetGameModesAsync();
    Task<GameModeResponseDto> GetGameModeAsync(int id);
    Task CreateGameModeAsync(CreateGameModeDto dto);
    Task UpdateGameModeAsync(int id, UpdateGameModeDto dto);
    Task DeleteGameModeAsync(int id);
}