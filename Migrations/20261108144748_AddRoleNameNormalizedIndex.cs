using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BE_ZSM.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleNameNormalizedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT UPPER([Name]) FROM [Roles] GROUP BY UPPER([Name]) HAVING COUNT(*) > 1)
    THROW 51200, 'Role names are duplicated without case sensitivity; normalize them before applying this migration.', 1;");

            migrationBuilder.AddColumn<string>(
                name: "NameNormalized",
                table: "Roles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                computedColumnSql: "UPPER([Name])",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_NameNormalized",
                table: "Roles",
                column: "NameNormalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_NameNormalized",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "NameNormalized",
                table: "Roles");
        }
    }
}
