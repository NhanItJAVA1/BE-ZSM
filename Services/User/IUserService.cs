using BE_ZSM.DTOs.Users;

namespace BE_ZSM.Services.Interfaces;

public interface IUserService
{
    Task<List<UserResponseDto>> GetUsersAsync();

    Task<UserResponseDto> GetUserAsync(int id);

    Task<UserResponseDto> RegisterAsync(RegisterUserDto dto);

    Task<LoginResponseDto> LoginAsync(LoginUserDto dto);

    Task<UserResponseDto> UpdateAsync(
        int id,
        UpdateUserDto dto);

    Task DeleteAsync(int id);

    Task<LoginResponseDto> RefreshTokenAsync(
        string refreshToken);
}