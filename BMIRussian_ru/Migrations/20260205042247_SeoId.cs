using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BMIRussian_ru.Migrations
{
    /// <inheritdoc />
    public partial class SeoId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SEOId",
                table: "Videos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SEOId",
                table: "Channels",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SEOId",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "SEOId",
                table: "Channels");
        }
    }
}
