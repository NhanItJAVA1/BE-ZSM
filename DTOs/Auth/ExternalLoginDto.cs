using BE_ZSM.Enums;

namespace BE_ZSM.DTOs.Auth
{
    public class ExternalLoginDto
    {
        public AuthProvider Provider { get; set; }
        public string Token { get; set; } = null!;
    }
}
