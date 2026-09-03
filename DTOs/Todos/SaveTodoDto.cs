using BE_ZSM.Enums;

namespace BE_ZSM.DTOs.Todos
{
    public class SaveTodoDto
    {
        public int? Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public TodoPriority? Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public int? CategoryId { get; set; }
        public bool IsDeleted { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
