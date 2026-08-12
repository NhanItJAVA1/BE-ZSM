namespace BE_ZSM.Entities
{
    public class RefreshToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreateAt { get; set; }
        public DateTime? RevokeAt { get; set; }
        public User User { get; set; } = null!;
    }
}
