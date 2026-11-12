namespace BE_ZSM.DTOs.AdminRecovery;

public sealed class AdminRecoveryUserDto
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
