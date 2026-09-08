using BE_ZSM.DTOs.Auth;
using BE_ZSM.DTOs.Users;

namespace BE_ZSM.Services.Auth;

public interface IAuthService
{
    Task RegisterAsync(RegisterUserDto dto);
    Task<LoginResponseDto> LoginAsync(LoginUserDto dto);
    Task<LoginResponseDto> ExternalLoginAsync(ExternalLoginDto dto);
    Task<LoginResponseDto> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
}
