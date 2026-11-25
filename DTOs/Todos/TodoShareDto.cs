namespace BE_ZSM.DTOs.Todos;

public sealed class TodoShareDto
{
    public int SharedWithUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
