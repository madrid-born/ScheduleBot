using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScheduleBot.Migrations
{
    /// <inheritdoc />
    public partial class MoveCycleNotificationsToNotificationService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NotifyMode",
                table: "NotificationAccess",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SpecialBehaviorTargetId",
                table: "Notification",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSDATETIME();
                DECLARE @TodayAt1230 datetime2 = DATEADD(MINUTE, 750, CONVERT(datetime2, CONVERT(date, @Now)));
                DECLARE @FirstOccurrence datetime2 =
                    CASE WHEN @TodayAt1230 <= @Now THEN DATEADD(DAY, 1, @TodayAt1230) ELSE @TodayAt1230 END;

                INSERT INTO [Notification]
                    ([Id], [UserId], [IsActive], [CreateTime], [StartTime], [Type], [SeparationValue],
                     [Name], [Message], [SpecialBehavior], [SpecialBehaviorTargetId])
                SELECT NEWID(), cycle.[UserId], 1, @Now, @FirstOccurrence, 2, 1,
                       N'Period Tracker', N'', 2, cycle.[Id]
                FROM [CycleDetails] cycle
                WHERE EXISTS (
                    SELECT 1 FROM [CycleNotifies] oldAccess WHERE oldAccess.[CycleId] = cycle.[Id]
                );

                INSERT INTO [NotificationFutureMessage] ([Id], [NotificationId], [Time], [Message])
                SELECT NEWID(), notification.[Id], @FirstOccurrence, NULL
                FROM [Notification] notification
                WHERE notification.[SpecialBehavior] = 2
                  AND notification.[SpecialBehaviorTargetId] IS NOT NULL;

                WITH ExistingAccess AS
                (
                    SELECT [CycleId], [ReceiverId], [NotifyMode],
                           ROW_NUMBER() OVER (PARTITION BY [CycleId], [ReceiverId] ORDER BY [Id]) AS [RowNumber]
                    FROM [CycleNotifies]
                )
                INSERT INTO [NotificationAccess] ([Id], [NotificationId], [UserId], [NotifyMode])
                SELECT NEWID(), notification.[Id], oldAccess.[ReceiverId], oldAccess.[NotifyMode]
                FROM ExistingAccess oldAccess
                INNER JOIN [Notification] notification
                    ON notification.[SpecialBehaviorTargetId] = oldAccess.[CycleId]
                   AND notification.[SpecialBehavior] = 2
                WHERE oldAccess.[RowNumber] = 1;
                """);

            migrationBuilder.DropTable(
                name: "CycleNotifies");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_SpecialBehavior_SpecialBehaviorTargetId",
                table: "Notification",
                columns: new[] { "SpecialBehavior", "SpecialBehaviorTargetId" },
                unique: true,
                filter: "[SpecialBehaviorTargetId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationAccess_NotificationId_UserId",
                table: "NotificationAccess",
                columns: new[] { "NotificationId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CycleNotifies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotifyMode = table.Column<int>(type: "int", nullable: false),
                    ReceiverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CycleNotifies", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [CycleNotifies] ([Id], [CycleId], [ReceiverId], [NotifyMode])
                SELECT NEWID(), notification.[SpecialBehaviorTargetId], access.[UserId], access.[NotifyMode]
                FROM [NotificationAccess] access
                INNER JOIN [Notification] notification ON notification.[Id] = access.[NotificationId]
                WHERE notification.[SpecialBehavior] = 2
                  AND notification.[SpecialBehaviorTargetId] IS NOT NULL;

                DELETE future
                FROM [NotificationFutureMessage] future
                INNER JOIN [Notification] notification ON notification.[Id] = future.[NotificationId]
                WHERE notification.[SpecialBehavior] = 2
                  AND notification.[SpecialBehaviorTargetId] IS NOT NULL;

                DELETE access
                FROM [NotificationAccess] access
                INNER JOIN [Notification] notification ON notification.[Id] = access.[NotificationId]
                WHERE notification.[SpecialBehavior] = 2
                  AND notification.[SpecialBehaviorTargetId] IS NOT NULL;

                DELETE FROM [Notification]
                WHERE [SpecialBehavior] = 2
                  AND [SpecialBehaviorTargetId] IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Notification_SpecialBehavior_SpecialBehaviorTargetId",
                table: "Notification");

            migrationBuilder.DropIndex(
                name: "IX_NotificationAccess_NotificationId_UserId",
                table: "NotificationAccess");

            migrationBuilder.DropColumn(
                name: "NotifyMode",
                table: "NotificationAccess");

            migrationBuilder.DropColumn(
                name: "SpecialBehaviorTargetId",
                table: "Notification");
        }
    }
}
