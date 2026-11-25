namespace BE_ZSM.Entities;

public class TodoShare
{
    public int TodoId { get; set; }
    public Todo Todo { get; set; } = null!;
    public int SharedWithUserId { get; set; }
    public User SharedWithUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
