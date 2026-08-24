using BE_ZSM.Enums;

namespace BE_ZSM.DTOs.Todos
{
    public class UpdateTodoDto
    {
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public TodoStatus Status { get; set; }

        public TodoPriority Priority { get; set; }

        public DateTime? DueDate { get; set; }
    }
}
