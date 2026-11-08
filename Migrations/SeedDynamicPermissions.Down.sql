BEGIN TRANSACTION;

IF EXISTS (
    SELECT 1
    FROM [RbacPermissionSeedHistory] seed
    INNER JOIN [RolePermissions] rp ON rp.[PermissionId] = seed.[PermissionId]
    LEFT JOIN [RbacPermissionSeedHistory] tracked
        ON tracked.[EntryType] = 2 AND tracked.[RoleId] = rp.[RoleId] AND tracked.[PermissionId] = rp.[PermissionId]
    WHERE seed.[EntryType] = 1 AND tracked.[Id] IS NULL)
    THROW 51102, 'A migration-seeded permission has additional role grants; rollback aborted.', 1;

DELETE rp
FROM [RolePermissions] rp
INNER JOIN [RbacPermissionSeedHistory] seed
    ON seed.[EntryType] = 2 AND seed.[RoleId] = rp.[RoleId] AND seed.[PermissionId] = rp.[PermissionId];

DELETE p
FROM [Permissions] p
INNER JOIN [RbacPermissionSeedHistory] seed
    ON seed.[EntryType] = 1 AND seed.[PermissionId] = p.[Id];

DROP TABLE [RbacPermissionSeedHistory];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261108142114_SeedDynamicPermissions';

COMMIT;
GO

