using BE_ZSM.Enums;

namespace BE_ZSM.DTOs.Todos
{
    public class TodoActivityDto
    {
        public int Id { get; set; }
        public TodoActivityType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
