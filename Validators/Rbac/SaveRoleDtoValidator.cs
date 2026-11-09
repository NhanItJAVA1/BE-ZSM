using BE_ZSM.DTOs.Rbac;
using FluentValidation;

namespace BE_ZSM.Validators.Rbac
{
    public class SaveRoleDtoValidator : AbstractValidator<SaveRoleDto>
    {
        public SaveRoleDtoValidator()
        {
            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .Must(name => !string.IsNullOrWhiteSpace(name))
                .WithMessage("Role name is required")
                .WithErrorCode("ROLE_NAME_REQUIRED")
                .Must(name => name!.Trim().Length <= 50)
                .WithMessage("Role name cannot exceed 50 characters")
                .WithErrorCode("ROLE_NAME_TOO_LONG");

            RuleFor(x => x.Description)
                .Must(description => (description?.Trim().Length ?? 0) <= 500)
                .WithMessage("Role description cannot exceed 500 characters")
                .WithErrorCode("ROLE_DESCRIPTION_TOO_LONG");
        }
    }

    public class ReplaceRolePermissionsDtoValidator : AbstractValidator<ReplaceRolePermissionsDto>
    {
        public ReplaceRolePermissionsDtoValidator()
        {
            RuleFor(x => x.PermissionIds)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("PermissionIds is required")
                .WithErrorCode("RBAC_IDS_INVALID")

                .Must(ids => ids!.All(id => id > 0))
                .WithMessage("PermissionIds must contain positive IDs")
                .WithErrorCode("RBAC_IDS_INVALID")

                .Must(ids => ids!.Distinct().Count() == ids.Count)
                .WithMessage("PermissionIds must not contain duplicates")
                .WithErrorCode("RBAC_IDS_DUPLICATE");
        }
    }

    public sealed class ReplaceUserRolesDtoValidator  : AbstractValidator<ReplaceUserRolesDto>
    {
        public ReplaceUserRolesDtoValidator()
        {
            RuleFor(x => x.RoleIds)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithErrorCode("RBAC_IDS_INVALID")

                .NotEmpty()
                .WithMessage("User must have at least one role")
                .WithErrorCode("USER_ROLE_REQUIRED")

                .Must(ids => ids!.All(id => id > 0))
                .WithMessage("RoleIds must contain positive IDs")
                .WithErrorCode("RBAC_IDS_INVALID")

                .Must(ids => ids!.Distinct().Count() == ids.Count)
                .WithMessage("RoleIds must not contain duplicates")
                .WithErrorCode("RBAC_IDS_DUPLICATE");
        }
    }
}
