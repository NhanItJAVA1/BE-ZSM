using System.ComponentModel.DataAnnotations;

namespace BE_ZSM.DTOs.Rbac;

public sealed class ReplaceUserRolesDto
{
    [Required]
    [MinLength(1)]
    public List<int> RoleIds { get; set; } = [];
}
