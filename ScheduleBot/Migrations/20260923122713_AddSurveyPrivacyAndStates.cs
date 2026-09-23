using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScheduleBot.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyPrivacyAndStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SurveyUserAnswer_UserId_SurveyId_QuestionId",
                table: "SurveyUserAnswer");

            migrationBuilder.AddColumn<int>(
                name: "StateIndex",
                table: "SurveyUserAnswer",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrivate",
                table: "Survey",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StateOneName",
                table: "Survey",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateTwoName",
                table: "Survey",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurveyUserAnswer_UserId_SurveyId_QuestionId_StateIndex",
                table: "SurveyUserAnswer",
                columns: new[] { "UserId", "SurveyId", "QuestionId", "StateIndex" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SurveyUserAnswer_StateIndex",
                table: "SurveyUserAnswer",
                sql: "[StateIndex] IN (0, 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Survey_States",
                table: "Survey",
                sql: "([StateOneName] IS NULL AND [StateTwoName] IS NULL) OR ([StateOneName] IS NOT NULL AND [StateTwoName] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SurveyUserAnswer_UserId_SurveyId_QuestionId_StateIndex",
                table: "SurveyUserAnswer");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SurveyUserAnswer_StateIndex",
                table: "SurveyUserAnswer");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Survey_States",
                table: "Survey");

            migrationBuilder.DropColumn(
                name: "StateIndex",
                table: "SurveyUserAnswer");

            migrationBuilder.DropColumn(
                name: "IsPrivate",
                table: "Survey");

            migrationBuilder.DropColumn(
                name: "StateOneName",
                table: "Survey");

            migrationBuilder.DropColumn(
                name: "StateTwoName",
                table: "Survey");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyUserAnswer_UserId_SurveyId_QuestionId",
                table: "SurveyUserAnswer",
                columns: new[] { "UserId", "SurveyId", "QuestionId" },
                unique: true);
        }
    }
}
