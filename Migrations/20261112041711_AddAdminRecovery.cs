using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BE_ZSM.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [Roles] WHERE [NameNormalized] = N'RBAC_RECOVERY_ADMIN')
    THROW 51001, N'Cannot create RBAC_RECOVERY_ADMIN: a role with that normalized name already exists. Resolve the collision manually before applying this migration.', 1;

IF (SELECT COUNT(*) FROM [Permissions] WHERE [Code] IN (N'roles.manage-permissions', N'users.manage-roles')) <> 2
    THROW 51002, N'Cannot create RBAC_RECOVERY_ADMIN: required RBAC permissions are missing.', 1;

DECLARE @RecoveryRoleId int = (SELECT ISNULL(MAX([Id]), 0) + 1 FROM [Roles]);
INSERT INTO [Roles] ([Id], [Name], [Description])
VALUES (@RecoveryRoleId, N'RBAC_RECOVERY_ADMIN', N'System-managed administrative recovery role');

INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
SELECT @RecoveryRoleId, [Id]
FROM [Permissions]
WHERE [Code] IN (N'roles.manage-permissions', N'users.manage-roles');
");

            migrationBuilder.CreateTable(
                name: "AdminRecoveryAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    TargetUserId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRecoveryAudits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminRecoveryAudits_ActorUserId",
                table: "AdminRecoveryAudits",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminRecoveryAudits_TargetUserId",
                table: "AdminRecoveryAudits",
                column: "TargetUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminRecoveryAudits");

            migrationBuilder.Sql(@"
DELETE rp FROM [RolePermissions] rp INNER JOIN [Roles] r ON r.[Id] = rp.[RoleId] WHERE r.[NameNormalized] = N'RBAC_RECOVERY_ADMIN';
DELETE ur FROM [UserRoles] ur INNER JOIN [Roles] r ON r.[Id] = ur.[RoleId] WHERE r.[NameNormalized] = N'RBAC_RECOVERY_ADMIN';
DELETE FROM [Roles] WHERE [NameNormalized] = N'RBAC_RECOVERY_ADMIN';
");
        }
    }
}
