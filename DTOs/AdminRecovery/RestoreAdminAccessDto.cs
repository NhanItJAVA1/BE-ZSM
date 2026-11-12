namespace BE_ZSM.DTOs.AdminRecovery;

public sealed class RestoreAdminAccessDto
{
    public int UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
