using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BE_ZSM.Migrations
{
    /// <inheritdoc />
    public partial class SeedRbacManagementPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Admin')
    THROW 51300, 'Required Admin role is missing; RBAC management permissions cannot be seeded.', 1;
IF OBJECT_ID(N'[RbacPermissionSeedHistory]', N'U') IS NULL
    THROW 51301, 'Permission seed history is missing; apply the Phase 3 permission seed migration first.', 1;
IF EXISTS (
    SELECT 1
    FROM [RolePermissions] rp
    INNER JOIN [Permissions] p ON p.[Id] = rp.[PermissionId]
    INNER JOIN [Roles] r ON r.[Id] = rp.[RoleId]
    WHERE p.[Code] IN (N'roles.read', N'roles.create', N'roles.update', N'roles.delete',
        N'roles.manage-permissions', N'users.manage-roles', N'permissions.read')
      AND r.[Name] <> N'Admin')
    THROW 51303, 'RBAC management permissions are already granted to a non-Admin role; review before seeding.', 1;

DECLARE @NewPermissions TABLE ([Id] int NOT NULL, [Code] nvarchar(100) NOT NULL);
INSERT INTO [Permissions] ([Code], [Description])
OUTPUT inserted.[Id], inserted.[Code] INTO @NewPermissions ([Id], [Code])
SELECT seed.[Code], seed.[Description]
FROM (VALUES
    (N'roles.read', N'View roles'),
    (N'roles.create', N'Create roles'),
    (N'roles.update', N'Update roles'),
    (N'roles.delete', N'Delete roles'),
    (N'roles.manage-permissions', N'Replace role permissions'),
    (N'users.manage-roles', N'Replace user roles'),
    (N'permissions.read', N'View permissions')
) AS seed([Code], [Description])
WHERE NOT EXISTS (SELECT 1 FROM [Permissions] p WHERE p.[Code] = seed.[Code]);

INSERT INTO [RbacPermissionSeedHistory] ([EntryType], [PermissionCode], [PermissionId])
SELECT 1, [Code], [Id] FROM @NewPermissions;

DECLARE @AdminId int = (SELECT TOP(1) [Id] FROM [Roles] WHERE [Name] = N'Admin');
DECLARE @NewGrants TABLE ([RoleId] int NOT NULL, [PermissionId] int NOT NULL);
INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
OUTPUT inserted.[RoleId], inserted.[PermissionId]
    INTO @NewGrants ([RoleId], [PermissionId])
SELECT @AdminId, p.[Id]
FROM [Permissions] p
WHERE p.[Code] IN (
        N'roles.read', N'roles.create', N'roles.update', N'roles.delete',
        N'roles.manage-permissions', N'users.manage-roles', N'permissions.read')
  AND NOT EXISTS (
        SELECT 1 FROM [RolePermissions] existing
        WHERE existing.[RoleId] = @AdminId AND existing.[PermissionId] = p.[Id]);

INSERT INTO [RbacPermissionSeedHistory] ([EntryType], [PermissionCode], [RoleId], [PermissionId])
SELECT 2, p.[Code], grantRow.[RoleId], grantRow.[PermissionId]
FROM @NewGrants grantRow
INNER JOIN [Permissions] p ON p.[Id] = grantRow.[PermissionId];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM [RbacPermissionSeedHistory] seed
    INNER JOIN [RolePermissions] rp ON rp.[PermissionId] = seed.[PermissionId]
    LEFT JOIN [RbacPermissionSeedHistory] tracked
        ON tracked.[EntryType] = 2 AND tracked.[RoleId] = rp.[RoleId] AND tracked.[PermissionId] = rp.[PermissionId]
    WHERE seed.[EntryType] = 1
      AND seed.[PermissionCode] IN (N'roles.read', N'roles.create', N'roles.update', N'roles.delete',
          N'roles.manage-permissions', N'users.manage-roles', N'permissions.read')
      AND tracked.[Id] IS NULL)
    THROW 51302, 'A newly seeded RBAC permission has additional role grants; rollback aborted.', 1;

DELETE rp
FROM [RolePermissions] rp
INNER JOIN [RbacPermissionSeedHistory] seed
    ON seed.[EntryType] = 2 AND seed.[RoleId] = rp.[RoleId] AND seed.[PermissionId] = rp.[PermissionId]
WHERE seed.[PermissionCode] IN (N'roles.read', N'roles.create', N'roles.update', N'roles.delete',
    N'roles.manage-permissions', N'users.manage-roles', N'permissions.read');

DELETE p
FROM [Permissions] p
INNER JOIN [RbacPermissionSeedHistory] seed
    ON seed.[EntryType] = 1 AND seed.[PermissionId] = p.[Id]
WHERE seed.[PermissionCode] IN (N'roles.read', N'roles.create', N'roles.update', N'roles.delete',
    N'roles.manage-permissions', N'users.manage-roles', N'permissions.read');

DELETE FROM [RbacPermissionSeedHistory]
WHERE [PermissionCode] IN (N'roles.read', N'roles.create', N'roles.update', N'roles.delete',
    N'roles.manage-permissions', N'users.manage-roles', N'permissions.read');");
        }
    }
}
