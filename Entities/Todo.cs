using BE_ZSM.Enums;

namespace BE_ZSM.Entities
{
    public class Todo
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public TodoStatus Status { get; set; }

        public TodoPriority Priority { get; set; }

        public DateTime? DueDate { get; set; }

        public int UserId { get; set; }

        public User User { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
