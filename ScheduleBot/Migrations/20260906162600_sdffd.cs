using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScheduleBot.Migrations
{
    /// <inheritdoc />
    public partial class sdffd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SpecialBehavior",
                table: "Notification",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpecialBehavior",
                table: "Notification");
        }
    }
}
