using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BE_ZSM.Migrations
{
    /// <inheritdoc />
    public partial class AddTodoSharingAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TodoNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TodoId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TodoNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TodoNotes_Todos_TodoId",
                        column: x => x.TodoId,
                        principalTable: "Todos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TodoNotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TodoShares",
                columns: table => new
                {
                    TodoId = table.Column<int>(type: "int", nullable: false),
                    SharedWithUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TodoShares", x => new { x.TodoId, x.SharedWithUserId });
                    table.ForeignKey(
                        name: "FK_TodoShares_Todos_TodoId",
                        column: x => x.TodoId,
                        principalTable: "Todos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TodoShares_Users_SharedWithUserId",
                        column: x => x.SharedWithUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TodoNotes_TodoId_CreatedAt_Id",
                table: "TodoNotes",
                columns: new[] { "TodoId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TodoNotes_UserId",
                table: "TodoNotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TodoShares_SharedWithUserId_TodoId",
                table: "TodoShares",
                columns: new[] { "SharedWithUserId", "TodoId" });

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[RbacPermissionSeedHistory]', N'U') IS NULL
    THROW 51500, 'Permission seed history is missing; apply the Dynamic RBAC permission seed migration first.', 1;

DECLARE @NewPermissions TABLE ([Id] int NOT NULL, [Code] nvarchar(100) NOT NULL);
INSERT INTO [Permissions] ([Code], [Description])
OUTPUT inserted.[Id], inserted.[Code] INTO @NewPermissions ([Id], [Code])
SELECT seed.[Code], seed.[Description]
FROM (VALUES
    (N'todos.share', N'Share todos with other users'),
    (N'todos.read-shared', N'Read todos shared with the current user'),
    (N'todos.notes.create', N'Create notes on accessible todos')
) AS seed([Code], [Description])
WHERE NOT EXISTS (SELECT 1 FROM [Permissions] permission WHERE permission.[Code] = seed.[Code]);

INSERT INTO [RbacPermissionSeedHistory] ([EntryType], [PermissionCode], [PermissionId])
SELECT 1, [Code], [Id] FROM @NewPermissions;");
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
    WHERE seed.[EntryType] = 1
      AND seed.[PermissionCode] IN (N'todos.share', N'todos.read-shared', N'todos.notes.create')
      AND tracked.[Id] IS NULL)
    THROW 51501, 'A seeded Todo sharing permission has additional role grants; rollback aborted.', 1;

DELETE permission
FROM [Permissions] permission
INNER JOIN [RbacPermissionSeedHistory] seed
    ON seed.[EntryType] = 1 AND seed.[PermissionId] = permission.[Id]
WHERE seed.[PermissionCode] IN (N'todos.share', N'todos.read-shared', N'todos.notes.create');

DELETE FROM [RbacPermissionSeedHistory]
WHERE [PermissionCode] IN (N'todos.share', N'todos.read-shared', N'todos.notes.create');");

            migrationBuilder.DropTable(
                name: "TodoNotes");

            migrationBuilder.DropTable(
                name: "TodoShares");
        }
    }
}
