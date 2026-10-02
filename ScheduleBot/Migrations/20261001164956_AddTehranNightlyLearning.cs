using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScheduleBot.Migrations
{
    /// <inheritdoc />
    public partial class AddTehranNightlyLearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLearnt",
                table: "TeaMapUserPlaceLearningProgress",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LearntAtUtc",
                table: "TeaMapUserPlaceLearningProgress",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NightId",
                table: "TeaMapQuizzes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferencePlaceId",
                table: "TeaMapQuizQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeaMapLearningNights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FirstPlaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecondPlaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearntCount = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeaMapLearningNights", x => x.Id);
                    table.CheckConstraint("CK_TeaMapLearningNight_Count", "[LearntCount] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_TeaMapLearningNights_TeaMapPlaces_FirstPlaceId",
                        column: x => x.FirstPlaceId,
                        principalTable: "TeaMapPlaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeaMapLearningNights_TeaMapPlaces_SecondPlaceId",
                        column: x => x.SecondPlaceId,
                        principalTable: "TeaMapPlaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeaMapLearningNights_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeaMapQuizzes_NightId",
                table: "TeaMapQuizzes",
                column: "NightId",
                unique: true,
                filter: "[NightId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeaMapQuizQuestions_ReferencePlaceId",
                table: "TeaMapQuizQuestions",
                column: "ReferencePlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_TeaMapLearningNights_FirstPlaceId",
                table: "TeaMapLearningNights",
                column: "FirstPlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_TeaMapLearningNights_SecondPlaceId",
                table: "TeaMapLearningNights",
                column: "SecondPlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_TeaMapLearningNights_UserId_LocalDate",
                table: "TeaMapLearningNights",
                columns: new[] { "UserId", "LocalDate" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapQuizQuestions_TeaMapPlaces_ReferencePlaceId",
                table: "TeaMapQuizQuestions",
                column: "ReferencePlaceId",
                principalTable: "TeaMapPlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapQuizzes_TeaMapLearningNights_NightId",
                table: "TeaMapQuizzes",
                column: "NightId",
                principalTable: "TeaMapLearningNights",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapQuizQuestions_TeaMapPlaces_ReferencePlaceId",
                table: "TeaMapQuizQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapQuizzes_TeaMapLearningNights_NightId",
                table: "TeaMapQuizzes");

            migrationBuilder.DropTable(
                name: "TeaMapLearningNights");

            migrationBuilder.DropIndex(
                name: "IX_TeaMapQuizzes_NightId",
                table: "TeaMapQuizzes");

            migrationBuilder.DropIndex(
                name: "IX_TeaMapQuizQuestions_ReferencePlaceId",
                table: "TeaMapQuizQuestions");

            migrationBuilder.DropColumn(
                name: "IsLearnt",
                table: "TeaMapUserPlaceLearningProgress");

            migrationBuilder.DropColumn(
                name: "LearntAtUtc",
                table: "TeaMapUserPlaceLearningProgress");

            migrationBuilder.DropColumn(
                name: "NightId",
                table: "TeaMapQuizzes");

            migrationBuilder.DropColumn(
                name: "ReferencePlaceId",
                table: "TeaMapQuizQuestions");
        }
    }
}
