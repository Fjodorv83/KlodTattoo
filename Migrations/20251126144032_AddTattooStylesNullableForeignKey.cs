using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlodTattooWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddTattooStylesNullableForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Style",
                table: "PortfolioItems");

            migrationBuilder.AddColumn<int>(
                name: "TattooStyleId",
                table: "PortfolioItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TattooStyles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TattooStyles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_TattooStyleId",
                table: "PortfolioItems",
                column: "TattooStyleId");

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioItems_TattooStyles_TattooStyleId",
                table: "PortfolioItems",
                column: "TattooStyleId",
                principalTable: "TattooStyles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioItems_TattooStyles_TattooStyleId",
                table: "PortfolioItems");

            migrationBuilder.DropTable(
                name: "TattooStyles");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioItems_TattooStyleId",
                table: "PortfolioItems");

            migrationBuilder.DropColumn(
                name: "TattooStyleId",
                table: "PortfolioItems");

            migrationBuilder.AddColumn<string>(
                name: "Style",
                table: "PortfolioItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
