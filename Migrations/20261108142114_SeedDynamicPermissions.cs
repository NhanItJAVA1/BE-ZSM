using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BE_ZSM.Migrations
{
    /// <inheritdoc />
    public partial class SeedDynamicPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RbacPermissionSeedHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntryType = table.Column<byte>(type: "tinyint", nullable: false),
                    PermissionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: true),
                    PermissionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_RbacPermissionSeedHistory", x => x.Id));

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'User')
    THROW 51100, 'Required User role is missing; permission seeding aborted.', 1;
IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Admin')
    THROW 51101, 'Required Admin role is missing; permission seeding aborted.', 1;

DECLARE @NewPermissions TABLE ([Id] int NOT NULL, [Code] nvarchar(100) NOT NULL);
INSERT INTO [Permissions] ([Code], [Description])
OUTPUT inserted.[Id], inserted.[Code] INTO @NewPermissions ([Id], [Code])
SELECT seed.[Code], seed.[Description]
FROM (VALUES
    (N'todo.read', N'Read todos'),
    (N'todo.write', N'Create, update, and delete todos'),
    (N'todo-category.read', N'Read todo categories'),
    (N'todo-category.create', N'Create todo categories'),
    (N'todo-category.update', N'Update todo categories'),
    (N'todo-category.delete', N'Delete todo categories'),
    (N'catalog.upload', N'Create catalog upload URLs'),
    (N'users.read', N'Read users'),
    (N'users.update', N'Update users'),
    (N'users.delete', N'Delete users')
) AS seed([Code], [Description])
WHERE NOT EXISTS (SELECT 1 FROM [Permissions] p WHERE p.[Code] = seed.[Code]);

INSERT INTO [RbacPermissionSeedHistory] ([EntryType], [PermissionCode], [PermissionId])
SELECT 1, [Code], [Id] FROM @NewPermissions;

DECLARE @SeedGrants TABLE ([RoleName] nvarchar(50) NOT NULL, [Code] nvarchar(100) NOT NULL);
INSERT INTO @SeedGrants ([RoleName], [Code]) VALUES
    (N'User', N'todo.read'), (N'User', N'todo.write'),
    (N'User', N'todo-category.read'), (N'User', N'todo-category.create'),
    (N'User', N'todo-category.update'), (N'User', N'todo-category.delete'),
    (N'Admin', N'todo.read'), (N'Admin', N'todo.write'),
    (N'Admin', N'todo-category.read'), (N'Admin', N'todo-category.create'),
    (N'Admin', N'todo-category.update'), (N'Admin', N'todo-category.delete'),
    (N'Admin', N'catalog.upload'), (N'Admin', N'users.read'),
    (N'Admin', N'users.update'), (N'Admin', N'users.delete');

DECLARE @NewGrants TABLE ([RoleId] int NOT NULL, [PermissionId] int NOT NULL);
INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
OUTPUT inserted.[RoleId], inserted.[PermissionId]
    INTO @NewGrants ([RoleId], [PermissionId])
SELECT r.[Id], p.[Id]
FROM @SeedGrants seed
INNER JOIN [Roles] r ON r.[Name] = seed.[RoleName]
INNER JOIN [Permissions] p ON p.[Code] = seed.[Code]
WHERE NOT EXISTS (
    SELECT 1 FROM [RolePermissions] existing
    WHERE existing.[RoleId] = r.[Id] AND existing.[PermissionId] = p.[Id]);

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
    WHERE seed.[EntryType] = 1 AND tracked.[Id] IS NULL)
    THROW 51102, 'A migration-seeded permission has additional role grants; rollback aborted.', 1;

DELETE rp
FROM [RolePermissions] rp
INNER JOIN [RbacPermissionSeedHistory] seed
    ON seed.[EntryType] = 2 AND seed.[RoleId] = rp.[RoleId] AND seed.[PermissionId] = rp.[PermissionId];

DELETE p
FROM [Permissions] p
INNER JOIN [RbacPermissionSeedHistory] seed
    ON seed.[EntryType] = 1 AND seed.[PermissionId] = p.[Id];");

            migrationBuilder.DropTable(name: "RbacPermissionSeedHistory");
        }
    }
}
