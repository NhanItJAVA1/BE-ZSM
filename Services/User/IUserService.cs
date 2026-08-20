using BE_ZSM.DTOs.Users;

namespace BE_ZSM.Services.Interfaces;

public interface IUserService
{
    Task<List<UserResponseDto>> GetUsersAsync();
    Task<UserResponseDto> GetUserAsync(int id);
    Task RegisterAsync(RegisterUserDto dto);
    Task<LoginResponseDto> LoginAsync(LoginUserDto dto);
    Task UpdateAsync(int id, UpdateUserDto dto);
    Task DeleteAsync(int id);
    Task<LoginResponseDto> RefreshTokenAsync(string refreshToken);
}