using BE_ZSM.Enums;

namespace BE_ZSM.DTOs.Todos
{
    public class CreateTodoDto
    {
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public TodoPriority Priority { get; set; } = TodoPriority.Medium;

        public DateTime? DueDate { get; set; }
        public int? CategoryId { get; set; }
    }
}
