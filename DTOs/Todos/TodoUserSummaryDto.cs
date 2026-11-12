namespace BE_ZSM.DTOs.Todos;

public sealed class TodoUserSummaryDto
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TodoCount { get; set; }
}
