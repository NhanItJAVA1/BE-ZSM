namespace BE_ZSM.DTOs.GameModes;

public class GameModeResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}