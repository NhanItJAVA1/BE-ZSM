namespace BE_ZSM.Services.Auth.Models
{
    public class ExternalUserInfo
    {
        public string ProviderUserId { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string? DisplayName { get; set; }

        public string? AvatarUrl { get; set; }

        public bool EmailVerified { get; set; }
    }
}
