using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BE_ZSM.Migrations
{
    /// <inheritdoc />
    public partial class ConvertRoleNameToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [Roles] WHERE [Name] NOT IN (0, 1))
    THROW 51001, 'Roles.Name contains an unsupported enum value; migration aborted.', 1;");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Roles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql(@"
UPDATE [Roles] SET [Name] = CASE [Name] WHEN N'0' THEN N'User' WHEN N'1' THEN N'Admin' END;
IF EXISTS (SELECT [Name] FROM [Roles] GROUP BY [Name] HAVING COUNT(*) > 1)
    THROW 51002, 'Role names would not be unique after enum conversion; migration aborted.', 1;");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [Roles] WHERE [Name] NOT IN (N'User', N'Admin'))
    THROW 51003, 'Roles.Name contains a value that cannot be converted back to the enum.', 1;
IF EXISTS (SELECT [Name] FROM [Roles] GROUP BY [Name] HAVING COUNT(*) > 1)
    THROW 51004, 'Duplicate role names prevent rollback.', 1;");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Name",
                table: "Roles");

            migrationBuilder.Sql(@"UPDATE [Roles] SET [Name] = CASE [Name] WHEN N'User' THEN N'0' WHEN N'Admin' THEN N'1' END;");

            migrationBuilder.AlterColumn<int>(
                name: "Name",
                table: "Roles",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

        }
    }
}
