using BE_ZSM.Enums;
using System.ComponentModel.DataAnnotations;

namespace BE_ZSM.DTOs.Todos
{
    public class SaveTodoDto
    {
        public int? Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }
        public TodoPriority? Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public int? CategoryId { get; set; }
        public bool IsDeleted { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
