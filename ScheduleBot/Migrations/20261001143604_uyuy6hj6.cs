using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScheduleBot.Migrations
{
    /// <inheritdoc />
    public partial class uyuy6hj6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LessonPlaces_Lessons_LessonId",
                table: "LessonPlaces");

            migrationBuilder.DropForeignKey(
                name: "FK_LessonPlaces_Places_PlaceId",
                table: "LessonPlaces");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaceAliases_Places_PlaceId",
                table: "PlaceAliases");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaceRelationships_Places_FromPlaceId",
                table: "PlaceRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaceRelationships_Places_ToPlaceId",
                table: "PlaceRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizAnswers_QuizQuestionOptions_SelectedOptionId",
                table: "QuizAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizAnswers_QuizQuestions_QuizQuestionId",
                table: "QuizAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizAnswers_Users_UserId",
                table: "QuizAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizQuestionOptions_QuizQuestions_QuizQuestionId",
                table: "QuizQuestionOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizQuestions_Places_PlaceId",
                table: "QuizQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizQuestions_Quizzes_QuizId",
                table: "QuizQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_Lessons_LessonId",
                table: "Quizzes");

            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_Users_UserId",
                table: "Quizzes");

            migrationBuilder.DropForeignKey(
                name: "FK_ScoreTransactions_Users_UserId",
                table: "ScoreTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_TehranGameProfiles_Users_UserId",
                table: "TehranGameProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPlaceLearningProgress_Places_PlaceId",
                table: "UserPlaceLearningProgress");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPlaceLearningProgress_Users_UserId",
                table: "UserPlaceLearningProgress");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserPlaceLearningProgress",
                table: "UserPlaceLearningProgress");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TehranGameProfiles",
                table: "TehranGameProfiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ScoreTransactions",
                table: "ScoreTransactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Quizzes",
                table: "Quizzes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuizQuestions",
                table: "QuizQuestions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuizQuestionOptions",
                table: "QuizQuestionOptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuizAnswers",
                table: "QuizAnswers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Places",
                table: "Places");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlaceRelationships",
                table: "PlaceRelationships");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlaceAliases",
                table: "PlaceAliases");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Lessons",
                table: "Lessons");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LessonPlaces",
                table: "LessonPlaces");

            migrationBuilder.RenameTable(
                name: "UserPlaceLearningProgress",
                newName: "TeaMapUserPlaceLearningProgress");

            migrationBuilder.RenameTable(
                name: "TehranGameProfiles",
                newName: "TeaMapTehranGameProfiles");

            migrationBuilder.RenameTable(
                name: "ScoreTransactions",
                newName: "TeaMapScoreTransactions");

            migrationBuilder.RenameTable(
                name: "Quizzes",
                newName: "TeaMapQuizzes");

            migrationBuilder.RenameTable(
                name: "QuizQuestions",
                newName: "TeaMapQuizQuestions");

            migrationBuilder.RenameTable(
                name: "QuizQuestionOptions",
                newName: "TeaMapQuizQuestionOptions");

            migrationBuilder.RenameTable(
                name: "QuizAnswers",
                newName: "TeaMapQuizAnswers");

            migrationBuilder.RenameTable(
                name: "Places",
                newName: "TeaMapPlaces");

            migrationBuilder.RenameTable(
                name: "PlaceRelationships",
                newName: "TeaMapPlaceRelationships");

            migrationBuilder.RenameTable(
                name: "PlaceAliases",
                newName: "TeaMapPlaceAliases");

            migrationBuilder.RenameTable(
                name: "Lessons",
                newName: "TeaMapLessons");

            migrationBuilder.RenameTable(
                name: "LessonPlaces",
                newName: "TeaMapLessonPlaces");

            migrationBuilder.RenameIndex(
                name: "IX_UserPlaceLearningProgress_UserId_PlaceId",
                table: "TeaMapUserPlaceLearningProgress",
                newName: "IX_TeaMapUserPlaceLearningProgress_UserId_PlaceId");

            migrationBuilder.RenameIndex(
                name: "IX_UserPlaceLearningProgress_PlaceId",
                table: "TeaMapUserPlaceLearningProgress",
                newName: "IX_TeaMapUserPlaceLearningProgress_PlaceId");

            migrationBuilder.RenameIndex(
                name: "IX_TehranGameProfiles_UserId",
                table: "TeaMapTehranGameProfiles",
                newName: "IX_TeaMapTehranGameProfiles_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_ScoreTransactions_UserId_CreatedAtUtc",
                table: "TeaMapScoreTransactions",
                newName: "IX_TeaMapScoreTransactions_UserId_CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_Quizzes_UserId_Status",
                table: "TeaMapQuizzes",
                newName: "IX_TeaMapQuizzes_UserId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_Quizzes_LessonId",
                table: "TeaMapQuizzes",
                newName: "IX_TeaMapQuizzes_LessonId");

            migrationBuilder.RenameIndex(
                name: "IX_QuizQuestions_QuizId_Position",
                table: "TeaMapQuizQuestions",
                newName: "IX_TeaMapQuizQuestions_QuizId_Position");

            migrationBuilder.RenameIndex(
                name: "IX_QuizQuestions_PlaceId",
                table: "TeaMapQuizQuestions",
                newName: "IX_TeaMapQuizQuestions_PlaceId");

            migrationBuilder.RenameIndex(
                name: "IX_QuizQuestionOptions_QuizQuestionId_Position",
                table: "TeaMapQuizQuestionOptions",
                newName: "IX_TeaMapQuizQuestionOptions_QuizQuestionId_Position");

            migrationBuilder.RenameIndex(
                name: "IX_QuizAnswers_UserId",
                table: "TeaMapQuizAnswers",
                newName: "IX_TeaMapQuizAnswers_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_QuizAnswers_SelectedOptionId",
                table: "TeaMapQuizAnswers",
                newName: "IX_TeaMapQuizAnswers_SelectedOptionId");

            migrationBuilder.RenameIndex(
                name: "IX_QuizAnswers_QuizQuestionId_UserId",
                table: "TeaMapQuizAnswers",
                newName: "IX_TeaMapQuizAnswers_QuizQuestionId_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Places_Source_ExternalId",
                table: "TeaMapPlaces",
                newName: "IX_TeaMapPlaces_Source_ExternalId");

            migrationBuilder.RenameIndex(
                name: "IX_Places_IsActive_Priority",
                table: "TeaMapPlaces",
                newName: "IX_TeaMapPlaces_IsActive_Priority");

            migrationBuilder.RenameIndex(
                name: "IX_PlaceRelationships_ToPlaceId",
                table: "TeaMapPlaceRelationships",
                newName: "IX_TeaMapPlaceRelationships_ToPlaceId");

            migrationBuilder.RenameIndex(
                name: "IX_PlaceRelationships_FromPlaceId_ToPlaceId_RelationshipType",
                table: "TeaMapPlaceRelationships",
                newName: "IX_TeaMapPlaceRelationships_FromPlaceId_ToPlaceId_RelationshipType");

            migrationBuilder.RenameIndex(
                name: "IX_PlaceAliases_PlaceId_Name_LanguageCode",
                table: "TeaMapPlaceAliases",
                newName: "IX_TeaMapPlaceAliases_PlaceId_Name_LanguageCode");

            migrationBuilder.RenameIndex(
                name: "IX_Lessons_IsPublished_Position",
                table: "TeaMapLessons",
                newName: "IX_TeaMapLessons_IsPublished_Position");

            migrationBuilder.RenameIndex(
                name: "IX_LessonPlaces_PlaceId",
                table: "TeaMapLessonPlaces",
                newName: "IX_TeaMapLessonPlaces_PlaceId");

            migrationBuilder.RenameIndex(
                name: "IX_LessonPlaces_LessonId_Position",
                table: "TeaMapLessonPlaces",
                newName: "IX_TeaMapLessonPlaces_LessonId_Position");

            migrationBuilder.RenameIndex(
                name: "IX_LessonPlaces_LessonId_PlaceId",
                table: "TeaMapLessonPlaces",
                newName: "IX_TeaMapLessonPlaces_LessonId_PlaceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeaMapUserPlaceLearningProgress",
                table: "TeaMapUserPlaceLearningProgress",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeaMapTehranGameProfiles",
                table: "TeaMapTehranGameProfiles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeaMapScoreTransactions",
                table: "TeaMapScoreTransactions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeaMapQuizzes",
                table: "TeaMapQuizzes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeaMapQuizQuestions",
                table: "TeaMapQuizQuestions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeaMapQuizQuestionOptions",
                table: "TeaMapQuizQuestionOptions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeaMapQuizAnswers",
                table: "TeaMapQuizAnswers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeaMapPlaces",
                table: "TeaMapPlaces",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeaMapPlaceRelationships",
                table: "TeaMapPlaceRelationships",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeaMapPlaceAliases",
                table: "TeaMapPlaceAliases",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeaMapLessons",
                table: "TeaMapLessons",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeaMapLessonPlaces",
                table: "TeaMapLessonPlaces",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapLessonPlaces_TeaMapLessons_LessonId",
                table: "TeaMapLessonPlaces",
                column: "LessonId",
                principalTable: "TeaMapLessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapLessonPlaces_TeaMapPlaces_PlaceId",
                table: "TeaMapLessonPlaces",
                column: "PlaceId",
                principalTable: "TeaMapPlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapPlaceAliases_TeaMapPlaces_PlaceId",
                table: "TeaMapPlaceAliases",
                column: "PlaceId",
                principalTable: "TeaMapPlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapPlaceRelationships_TeaMapPlaces_FromPlaceId",
                table: "TeaMapPlaceRelationships",
                column: "FromPlaceId",
                principalTable: "TeaMapPlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapPlaceRelationships_TeaMapPlaces_ToPlaceId",
                table: "TeaMapPlaceRelationships",
                column: "ToPlaceId",
                principalTable: "TeaMapPlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapQuizAnswers_TeaMapQuizQuestionOptions_SelectedOptionId",
                table: "TeaMapQuizAnswers",
                column: "SelectedOptionId",
                principalTable: "TeaMapQuizQuestionOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapQuizAnswers_TeaMapQuizQuestions_QuizQuestionId",
                table: "TeaMapQuizAnswers",
                column: "QuizQuestionId",
                principalTable: "TeaMapQuizQuestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapQuizAnswers_Users_UserId",
                table: "TeaMapQuizAnswers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapQuizQuestionOptions_TeaMapQuizQuestions_QuizQuestionId",
                table: "TeaMapQuizQuestionOptions",
                column: "QuizQuestionId",
                principalTable: "TeaMapQuizQuestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapQuizQuestions_TeaMapPlaces_PlaceId",
                table: "TeaMapQuizQuestions",
                column: "PlaceId",
                principalTable: "TeaMapPlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapQuizQuestions_TeaMapQuizzes_QuizId",
                table: "TeaMapQuizQuestions",
                column: "QuizId",
                principalTable: "TeaMapQuizzes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapQuizzes_TeaMapLessons_LessonId",
                table: "TeaMapQuizzes",
                column: "LessonId",
                principalTable: "TeaMapLessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapQuizzes_Users_UserId",
                table: "TeaMapQuizzes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapScoreTransactions_Users_UserId",
                table: "TeaMapScoreTransactions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapTehranGameProfiles_Users_UserId",
                table: "TeaMapTehranGameProfiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapUserPlaceLearningProgress_TeaMapPlaces_PlaceId",
                table: "TeaMapUserPlaceLearningProgress",
                column: "PlaceId",
                principalTable: "TeaMapPlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeaMapUserPlaceLearningProgress_Users_UserId",
                table: "TeaMapUserPlaceLearningProgress",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapLessonPlaces_TeaMapLessons_LessonId",
                table: "TeaMapLessonPlaces");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapLessonPlaces_TeaMapPlaces_PlaceId",
                table: "TeaMapLessonPlaces");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapPlaceAliases_TeaMapPlaces_PlaceId",
                table: "TeaMapPlaceAliases");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapPlaceRelationships_TeaMapPlaces_FromPlaceId",
                table: "TeaMapPlaceRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapPlaceRelationships_TeaMapPlaces_ToPlaceId",
                table: "TeaMapPlaceRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapQuizAnswers_TeaMapQuizQuestionOptions_SelectedOptionId",
                table: "TeaMapQuizAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapQuizAnswers_TeaMapQuizQuestions_QuizQuestionId",
                table: "TeaMapQuizAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapQuizAnswers_Users_UserId",
                table: "TeaMapQuizAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapQuizQuestionOptions_TeaMapQuizQuestions_QuizQuestionId",
                table: "TeaMapQuizQuestionOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapQuizQuestions_TeaMapPlaces_PlaceId",
                table: "TeaMapQuizQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapQuizQuestions_TeaMapQuizzes_QuizId",
                table: "TeaMapQuizQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapQuizzes_TeaMapLessons_LessonId",
                table: "TeaMapQuizzes");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapQuizzes_Users_UserId",
                table: "TeaMapQuizzes");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapScoreTransactions_Users_UserId",
                table: "TeaMapScoreTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapTehranGameProfiles_Users_UserId",
                table: "TeaMapTehranGameProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapUserPlaceLearningProgress_TeaMapPlaces_PlaceId",
                table: "TeaMapUserPlaceLearningProgress");

            migrationBuilder.DropForeignKey(
                name: "FK_TeaMapUserPlaceLearningProgress_Users_UserId",
                table: "TeaMapUserPlaceLearningProgress");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeaMapUserPlaceLearningProgress",
                table: "TeaMapUserPlaceLearningProgress");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeaMapTehranGameProfiles",
                table: "TeaMapTehranGameProfiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeaMapScoreTransactions",
                table: "TeaMapScoreTransactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeaMapQuizzes",
                table: "TeaMapQuizzes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeaMapQuizQuestions",
                table: "TeaMapQuizQuestions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeaMapQuizQuestionOptions",
                table: "TeaMapQuizQuestionOptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeaMapQuizAnswers",
                table: "TeaMapQuizAnswers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeaMapPlaces",
                table: "TeaMapPlaces");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeaMapPlaceRelationships",
                table: "TeaMapPlaceRelationships");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeaMapPlaceAliases",
                table: "TeaMapPlaceAliases");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeaMapLessons",
                table: "TeaMapLessons");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeaMapLessonPlaces",
                table: "TeaMapLessonPlaces");

            migrationBuilder.RenameTable(
                name: "TeaMapUserPlaceLearningProgress",
                newName: "UserPlaceLearningProgress");

            migrationBuilder.RenameTable(
                name: "TeaMapTehranGameProfiles",
                newName: "TehranGameProfiles");

            migrationBuilder.RenameTable(
                name: "TeaMapScoreTransactions",
                newName: "ScoreTransactions");

            migrationBuilder.RenameTable(
                name: "TeaMapQuizzes",
                newName: "Quizzes");

            migrationBuilder.RenameTable(
                name: "TeaMapQuizQuestions",
                newName: "QuizQuestions");

            migrationBuilder.RenameTable(
                name: "TeaMapQuizQuestionOptions",
                newName: "QuizQuestionOptions");

            migrationBuilder.RenameTable(
                name: "TeaMapQuizAnswers",
                newName: "QuizAnswers");

            migrationBuilder.RenameTable(
                name: "TeaMapPlaces",
                newName: "Places");

            migrationBuilder.RenameTable(
                name: "TeaMapPlaceRelationships",
                newName: "PlaceRelationships");

            migrationBuilder.RenameTable(
                name: "TeaMapPlaceAliases",
                newName: "PlaceAliases");

            migrationBuilder.RenameTable(
                name: "TeaMapLessons",
                newName: "Lessons");

            migrationBuilder.RenameTable(
                name: "TeaMapLessonPlaces",
                newName: "LessonPlaces");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapUserPlaceLearningProgress_UserId_PlaceId",
                table: "UserPlaceLearningProgress",
                newName: "IX_UserPlaceLearningProgress_UserId_PlaceId");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapUserPlaceLearningProgress_PlaceId",
                table: "UserPlaceLearningProgress",
                newName: "IX_UserPlaceLearningProgress_PlaceId");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapTehranGameProfiles_UserId",
                table: "TehranGameProfiles",
                newName: "IX_TehranGameProfiles_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapScoreTransactions_UserId_CreatedAtUtc",
                table: "ScoreTransactions",
                newName: "IX_ScoreTransactions_UserId_CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapQuizzes_UserId_Status",
                table: "Quizzes",
                newName: "IX_Quizzes_UserId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapQuizzes_LessonId",
                table: "Quizzes",
                newName: "IX_Quizzes_LessonId");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapQuizQuestions_QuizId_Position",
                table: "QuizQuestions",
                newName: "IX_QuizQuestions_QuizId_Position");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapQuizQuestions_PlaceId",
                table: "QuizQuestions",
                newName: "IX_QuizQuestions_PlaceId");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapQuizQuestionOptions_QuizQuestionId_Position",
                table: "QuizQuestionOptions",
                newName: "IX_QuizQuestionOptions_QuizQuestionId_Position");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapQuizAnswers_UserId",
                table: "QuizAnswers",
                newName: "IX_QuizAnswers_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapQuizAnswers_SelectedOptionId",
                table: "QuizAnswers",
                newName: "IX_QuizAnswers_SelectedOptionId");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapQuizAnswers_QuizQuestionId_UserId",
                table: "QuizAnswers",
                newName: "IX_QuizAnswers_QuizQuestionId_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapPlaces_Source_ExternalId",
                table: "Places",
                newName: "IX_Places_Source_ExternalId");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapPlaces_IsActive_Priority",
                table: "Places",
                newName: "IX_Places_IsActive_Priority");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapPlaceRelationships_ToPlaceId",
                table: "PlaceRelationships",
                newName: "IX_PlaceRelationships_ToPlaceId");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapPlaceRelationships_FromPlaceId_ToPlaceId_RelationshipType",
                table: "PlaceRelationships",
                newName: "IX_PlaceRelationships_FromPlaceId_ToPlaceId_RelationshipType");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapPlaceAliases_PlaceId_Name_LanguageCode",
                table: "PlaceAliases",
                newName: "IX_PlaceAliases_PlaceId_Name_LanguageCode");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapLessons_IsPublished_Position",
                table: "Lessons",
                newName: "IX_Lessons_IsPublished_Position");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapLessonPlaces_PlaceId",
                table: "LessonPlaces",
                newName: "IX_LessonPlaces_PlaceId");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapLessonPlaces_LessonId_Position",
                table: "LessonPlaces",
                newName: "IX_LessonPlaces_LessonId_Position");

            migrationBuilder.RenameIndex(
                name: "IX_TeaMapLessonPlaces_LessonId_PlaceId",
                table: "LessonPlaces",
                newName: "IX_LessonPlaces_LessonId_PlaceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserPlaceLearningProgress",
                table: "UserPlaceLearningProgress",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TehranGameProfiles",
                table: "TehranGameProfiles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ScoreTransactions",
                table: "ScoreTransactions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Quizzes",
                table: "Quizzes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_QuizQuestions",
                table: "QuizQuestions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_QuizQuestionOptions",
                table: "QuizQuestionOptions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_QuizAnswers",
                table: "QuizAnswers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Places",
                table: "Places",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlaceRelationships",
                table: "PlaceRelationships",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlaceAliases",
                table: "PlaceAliases",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Lessons",
                table: "Lessons",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LessonPlaces",
                table: "LessonPlaces",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LessonPlaces_Lessons_LessonId",
                table: "LessonPlaces",
                column: "LessonId",
                principalTable: "Lessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LessonPlaces_Places_PlaceId",
                table: "LessonPlaces",
                column: "PlaceId",
                principalTable: "Places",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaceAliases_Places_PlaceId",
                table: "PlaceAliases",
                column: "PlaceId",
                principalTable: "Places",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaceRelationships_Places_FromPlaceId",
                table: "PlaceRelationships",
                column: "FromPlaceId",
                principalTable: "Places",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaceRelationships_Places_ToPlaceId",
                table: "PlaceRelationships",
                column: "ToPlaceId",
                principalTable: "Places",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizAnswers_QuizQuestionOptions_SelectedOptionId",
                table: "QuizAnswers",
                column: "SelectedOptionId",
                principalTable: "QuizQuestionOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizAnswers_QuizQuestions_QuizQuestionId",
                table: "QuizAnswers",
                column: "QuizQuestionId",
                principalTable: "QuizQuestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizAnswers_Users_UserId",
                table: "QuizAnswers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizQuestionOptions_QuizQuestions_QuizQuestionId",
                table: "QuizQuestionOptions",
                column: "QuizQuestionId",
                principalTable: "QuizQuestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizQuestions_Places_PlaceId",
                table: "QuizQuestions",
                column: "PlaceId",
                principalTable: "Places",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizQuestions_Quizzes_QuizId",
                table: "QuizQuestions",
                column: "QuizId",
                principalTable: "Quizzes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_Lessons_LessonId",
                table: "Quizzes",
                column: "LessonId",
                principalTable: "Lessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_Users_UserId",
                table: "Quizzes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScoreTransactions_Users_UserId",
                table: "ScoreTransactions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TehranGameProfiles_Users_UserId",
                table: "TehranGameProfiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPlaceLearningProgress_Places_PlaceId",
                table: "UserPlaceLearningProgress",
                column: "PlaceId",
                principalTable: "Places",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPlaceLearningProgress_Users_UserId",
                table: "UserPlaceLearningProgress",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
