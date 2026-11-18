namespace BE_ZSM.DTOs.Rbac;

public sealed class BulkRemoveRoleUsersDto
{
    public List<int> UserIds { get; set; } = [];
}
