namespace BE_ZSM.Entities;

public class TodoNote
{
    public int Id { get; set; }
    public int TodoId { get; set; }
    public Todo Todo { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
