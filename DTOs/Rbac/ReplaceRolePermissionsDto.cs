using System.ComponentModel.DataAnnotations;

namespace BE_ZSM.DTOs.Rbac;

public sealed class ReplaceRolePermissionsDto
{
    [Required]
    public List<int> PermissionIds { get; set; } = [];
}
