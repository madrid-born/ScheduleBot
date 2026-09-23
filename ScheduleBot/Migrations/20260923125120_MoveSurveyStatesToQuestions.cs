using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScheduleBot.Migrations
{
    /// <inheritdoc />
    public partial class MoveSurveyStatesToQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StateOneName",
                table: "SurveyQuestion",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateTwoName",
                table: "SurveyQuestion",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // Preserve surveys created before states became question-specific.
            migrationBuilder.Sql("""
                UPDATE q
                SET q.StateOneName = s.StateOneName,
                    q.StateTwoName = s.StateTwoName
                FROM SurveyQuestion AS q
                INNER JOIN Survey AS s ON s.Id = q.SurveyId
                WHERE s.StateOneName IS NOT NULL AND s.StateTwoName IS NOT NULL;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Survey_States",
                table: "Survey");

            migrationBuilder.DropColumn(
                name: "StateOneName",
                table: "Survey");

            migrationBuilder.DropColumn(
                name: "StateTwoName",
                table: "Survey");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SurveyQuestion_States",
                table: "SurveyQuestion",
                sql: "([StateOneName] IS NULL AND [StateTwoName] IS NULL) OR ([StateOneName] IS NOT NULL AND [StateTwoName] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SurveyQuestion_States",
                table: "SurveyQuestion");

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

            // A survey-level rollback can retain only one pair; use the first stateful question.
            migrationBuilder.Sql("""
                UPDATE s
                SET s.StateOneName = states.StateOneName,
                    s.StateTwoName = states.StateTwoName
                FROM Survey AS s
                OUTER APPLY (
                    SELECT TOP (1) q.StateOneName, q.StateTwoName
                    FROM SurveyQuestion AS q
                    WHERE q.SurveyId = s.Id AND q.StateOneName IS NOT NULL AND q.StateTwoName IS NOT NULL
                    ORDER BY q.Position
                ) AS states;
                """);

            migrationBuilder.DropColumn(
                name: "StateOneName",
                table: "SurveyQuestion");

            migrationBuilder.DropColumn(
                name: "StateTwoName",
                table: "SurveyQuestion");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Survey_States",
                table: "Survey",
                sql: "([StateOneName] IS NULL AND [StateTwoName] IS NULL) OR ([StateOneName] IS NOT NULL AND [StateTwoName] IS NOT NULL)");
        }
    }
}
