using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BMIRussian_ru.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                table: "Videos",
                type: "interval",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Videos");
        }
    }
}
