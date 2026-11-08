BEGIN TRANSACTION;

IF EXISTS (SELECT UPPER([Name]) FROM [Roles] GROUP BY UPPER([Name]) HAVING COUNT(*) > 1)
    THROW 51200, 'Role names are duplicated without case sensitivity; normalize them before applying this migration.', 1;

ALTER TABLE [Roles] ADD [NameNormalized] AS UPPER([Name]) PERSISTED;

CREATE UNIQUE INDEX [IX_Roles_NameNormalized] ON [Roles] ([NameNormalized]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261108144748_AddRoleNameNormalizedIndex', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;

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
INNER JOIN [Permissions] p ON p.[Id] = grantRow.[PermissionId];

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261108144822_SeedRbacManagementPermissions', N'10.0.10');

COMMIT;
GO

