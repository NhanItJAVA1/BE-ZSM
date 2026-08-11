namespace BE_ZSM.DTOs.Users
{
    public class RegisterUserDto
    {
        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }
    }
}