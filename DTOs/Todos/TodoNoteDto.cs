namespace BE_ZSM.DTOs.Todos;

public sealed class TodoNoteDto
{
    public int Id { get; set; }
    public int TodoId { get; set; }
    public int UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
