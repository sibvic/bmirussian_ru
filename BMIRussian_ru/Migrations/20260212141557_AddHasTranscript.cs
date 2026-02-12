using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BMIRussian_ru.Migrations
{
    /// <inheritdoc />
    public partial class AddHasTranscript : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasTranscript",
                table: "Videos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasTranscript",
                table: "Videos");
        }
    }
}
