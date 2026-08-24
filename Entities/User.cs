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

        public int RoleId { get; set; }

        public Role Role { get; set; } = null!;
            
        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<Record> Records { get; set; } = new List<Record>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<Todo> Todos { get; set; } = [];
        public ICollection<TodoCategory> TodoCategories { get; set; } = [];
    }
}
