using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BE_ZSM.Migrations
{
    /// <inheritdoc />
    public partial class AddTodoCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Todo_Users_UserId",
                table: "Todo");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Todo",
                table: "Todo");

            migrationBuilder.RenameTable(
                name: "Todo",
                newName: "Todos");

            migrationBuilder.RenameIndex(
                name: "IX_Todo_UserId",
                table: "Todos",
                newName: "IX_Todos_UserId");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "Todos",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Todos",
                table: "Todos",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "TodoCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TodoCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TodoCategories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Todos_CategoryId",
                table: "Todos",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TodoCategories_UserId",
                table: "TodoCategories",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Todos_TodoCategories_CategoryId",
                table: "Todos",
                column: "CategoryId",
                principalTable: "TodoCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Todos_Users_UserId",
                table: "Todos",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Todos_TodoCategories_CategoryId",
                table: "Todos");

            migrationBuilder.DropForeignKey(
                name: "FK_Todos_Users_UserId",
                table: "Todos");

            migrationBuilder.DropTable(
                name: "TodoCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Todos",
                table: "Todos");

            migrationBuilder.DropIndex(
                name: "IX_Todos_CategoryId",
                table: "Todos");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Todos");

            migrationBuilder.RenameTable(
                name: "Todos",
                newName: "Todo");

            migrationBuilder.RenameIndex(
                name: "IX_Todos_UserId",
                table: "Todo",
                newName: "IX_Todo_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Todo",
                table: "Todo",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Todo_Users_UserId",
                table: "Todo",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
