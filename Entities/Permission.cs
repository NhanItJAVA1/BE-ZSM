namespace BE_ZSM.Entities;

public class Permission
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
