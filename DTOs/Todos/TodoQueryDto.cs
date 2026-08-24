using BE_ZSM.Enums;

namespace BE_ZSM.DTOs.Todos
{
    public class TodoQueryDto
    {
        public string? Search { get; set; }

        public TodoStatus? Status { get; set; }

        public TodoPriority? Priority { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 10;

        public string? SortBy { get; set; }

        public bool IsDescending { get; set; }
        public bool? IsOverdue { get; set; }
        public int? CategoryId { get; set; }
    }
}
