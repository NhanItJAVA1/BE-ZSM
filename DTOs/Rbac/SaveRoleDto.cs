using System.ComponentModel.DataAnnotations;

namespace BE_ZSM.DTOs.Rbac;

public sealed class SaveRoleDto
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
}
