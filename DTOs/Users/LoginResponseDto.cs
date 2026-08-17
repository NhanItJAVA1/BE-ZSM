using BE_ZSM.Entities;

namespace BE_ZSM.DTOs.Users
{
    public class LoginResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public UserResponseDto User { get; set; } = null!;
    }
}
