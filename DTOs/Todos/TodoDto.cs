using BE_ZSM.Enums;

namespace BE_ZSM.DTOs.Todos
{
    public class TodoDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public TodoStatus Status { get; set; }

        public TodoPriority Priority { get; set; }

        public DateTime? DueDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public bool IsOverdue { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsCompletedLate { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
    }
}
