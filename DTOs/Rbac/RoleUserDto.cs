namespace BE_ZSM.DTOs.Rbac;

public sealed class RoleUserDto
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
