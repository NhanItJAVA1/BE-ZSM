using BE_ZSM.Enums;

namespace BE_ZSM.Entities
{
    public class ExternalLogin
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public AuthProvider Provider { get; set; }
        public string ProviderUserId { get; set; } = null!;

        public string? Email { get; set; }

        public DateTime CreatedAt { get; set; }

        public User User { get; set; } = null!;
    }
}
