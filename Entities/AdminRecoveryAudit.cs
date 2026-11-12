namespace BE_ZSM.Entities;

public sealed class AdminRecoveryAudit
{
    public long Id { get; set; }
    public int ActorUserId { get; set; }
    public int TargetUserId { get; set; }
    public string Action { get; set; } = "ADMIN_ACCESS_RESTORED";
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string Outcome { get; set; } = string.Empty;
}
