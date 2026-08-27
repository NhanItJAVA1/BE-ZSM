using BE_ZSM.Enums;
namespace BE_ZSM.DTOs.Todos
{
    public class TodoRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TodoPriority? Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public int? CategoryId { get; set; }
    }
}
