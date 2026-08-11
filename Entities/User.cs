namespace BE_ZSM.Entities
{
    public class User
    {
        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<Record> Records { get; set; } = new List<Record>();
    }
}
