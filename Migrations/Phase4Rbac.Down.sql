BEGIN TRANSACTION;

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
    N'roles.manage-permissions', N'users.manage-roles', N'permissions.read');

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261108144822_SeedRbacManagementPermissions';

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_Roles_NameNormalized] ON [Roles];

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Roles]') AND [c].[name] = N'NameNormalized');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Roles] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [Roles] DROP COLUMN [NameNormalized];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261108144748_AddRoleNameNormalizedIndex';

COMMIT;
GO

