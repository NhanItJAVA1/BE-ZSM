using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BE_ZSM.Migrations
{
    /// <inheritdoc />
    public partial class SeedTodoAuthorizationPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[RbacPermissionSeedHistory]', N'U') IS NULL
    THROW 51400, 'Permission seed history is missing; apply the Dynamic RBAC permission seed migration first.', 1;

DECLARE @NewPermissions TABLE ([Id] int NOT NULL, [Code] nvarchar(100) NOT NULL);
INSERT INTO [Permissions] ([Code], [Description])
OUTPUT inserted.[Id], inserted.[Code] INTO @NewPermissions ([Id], [Code])
SELECT seed.[Code], seed.[Description]
FROM (VALUES
    (N'todos.read-all', N'Read todos across all users')
) AS seed([Code], [Description])
WHERE NOT EXISTS (SELECT 1 FROM [Permissions] permission WHERE permission.[Code] = seed.[Code]);

INSERT INTO [RbacPermissionSeedHistory] ([EntryType], [PermissionCode], [PermissionId])
SELECT 1, [Code], [Id] FROM @NewPermissions;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM [RbacPermissionSeedHistory] seed
    INNER JOIN [RolePermissions] assignment ON assignment.[PermissionId] = seed.[PermissionId]
    LEFT JOIN [RbacPermissionSeedHistory] tracked
        ON tracked.[EntryType] = 2 AND tracked.[RoleId] = assignment.[RoleId] AND tracked.[PermissionId] = assignment.[PermissionId]
    WHERE seed.[EntryType] = 1 AND seed.[PermissionCode] = N'todos.read-all'
      AND tracked.[Id] IS NULL)
    THROW 51403, 'A seeded Todo read permission has additional role grants; rollback aborted.', 1;

DELETE permission
FROM [Permissions] permission
INNER JOIN [RbacPermissionSeedHistory] seed
    ON seed.[EntryType] = 1 AND seed.[PermissionId] = permission.[Id]
WHERE seed.[PermissionCode] = N'todos.read-all';

DELETE FROM [RbacPermissionSeedHistory]
WHERE [PermissionCode] = N'todos.read-all';");
        }
    }
}
