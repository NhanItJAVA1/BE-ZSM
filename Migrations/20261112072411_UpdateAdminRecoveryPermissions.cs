using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BE_ZSM.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdminRecoveryPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @RecoveryRoleId int = (SELECT [Id] FROM [Roles] WHERE [NameNormalized] = N'RBAC_RECOVERY_ADMIN');
IF @RecoveryRoleId IS NULL
    THROW 51003, N'Cannot update RBAC_RECOVERY_ADMIN permissions: the Recovery Role does not exist.', 1;

IF (SELECT COUNT(*) FROM [Permissions] WHERE [Code] IN
    (N'roles.read', N'permissions.read', N'roles.manage-permissions', N'users.manage-roles')) <> 4
    THROW 51004, N'Cannot update RBAC_RECOVERY_ADMIN permissions: one or more required permissions are missing.', 1;

DELETE assignment
FROM [RolePermissions] assignment
WHERE assignment.[RoleId] = @RecoveryRoleId
  AND assignment.[PermissionId] NOT IN
  (
      SELECT [Id] FROM [Permissions]
      WHERE [Code] IN (N'roles.read', N'permissions.read', N'roles.manage-permissions', N'users.manage-roles')
  );

INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
SELECT @RecoveryRoleId, permission.[Id]
FROM [Permissions] permission
WHERE permission.[Code] IN
    (N'roles.read', N'permissions.read', N'roles.manage-permissions', N'users.manage-roles')
  AND NOT EXISTS
    (SELECT 1 FROM [RolePermissions] existing
     WHERE existing.[RoleId] = @RecoveryRoleId AND existing.[PermissionId] = permission.[Id]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @RecoveryRoleId int = (SELECT [Id] FROM [Roles] WHERE [NameNormalized] = N'RBAC_RECOVERY_ADMIN');
IF @RecoveryRoleId IS NOT NULL
BEGIN
    DELETE assignment
    FROM [RolePermissions] assignment
    INNER JOIN [Permissions] permission ON permission.[Id] = assignment.[PermissionId]
    WHERE assignment.[RoleId] = @RecoveryRoleId
      AND permission.[Code] IN (N'roles.read', N'permissions.read');
END;
");
        }
    }
}
