using BE_ZSM.Enums;

namespace BE_ZSM.Entities
{
    public class TodoActivity
    {
        public int Id { get; set; }
        public int TodoId { get; set; }
        public Todo Todo { get; set; } = null!;
        public TodoActivityType Type { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
