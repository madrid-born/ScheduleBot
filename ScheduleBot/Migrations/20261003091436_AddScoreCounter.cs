using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScheduleBot.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScoreGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitationCode = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoreGroups_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScoreMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoreMembers_ScoreGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ScoreGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScoreMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScoreRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitiatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreRequests", x => x.Id);
                    table.CheckConstraint("CK_ScoreRequest_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_ScoreRequests_ScoreGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ScoreGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScoreRequests_Users_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScoreRequests_Users_InitiatorId",
                        column: x => x.InitiatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScoreRequests_Users_ToUserId",
                        column: x => x.ToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScoreApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Accepted = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoreApprovals_ScoreRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ScoreRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScoreApprovals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreApprovals_RequestId_UserId",
                table: "ScoreApprovals",
                columns: new[] { "RequestId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoreApprovals_UserId",
                table: "ScoreApprovals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreGroups_CreatorId",
                table: "ScoreGroups",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreGroups_InvitationCode",
                table: "ScoreGroups",
                column: "InvitationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoreMembers_GroupId_UserId",
                table: "ScoreMembers",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoreMembers_UserId",
                table: "ScoreMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreRequests_FromUserId",
                table: "ScoreRequests",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreRequests_GroupId_CreatedAtUtc",
                table: "ScoreRequests",
                columns: new[] { "GroupId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreRequests_InitiatorId",
                table: "ScoreRequests",
                column: "InitiatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreRequests_ToUserId",
                table: "ScoreRequests",
                column: "ToUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScoreApprovals");

            migrationBuilder.DropTable(
                name: "ScoreMembers");

            migrationBuilder.DropTable(
                name: "ScoreRequests");

            migrationBuilder.DropTable(
                name: "ScoreGroups");
        }
    }
}
