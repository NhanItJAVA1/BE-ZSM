namespace BE_ZSM.DTOs.Rbac;

public sealed class PermissionResponseDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
