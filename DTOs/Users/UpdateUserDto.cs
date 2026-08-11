namespace BE_ZSM.DTOs.Users
{
    public class UpdateUserDto
    {
        public string Email { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }
    }
}