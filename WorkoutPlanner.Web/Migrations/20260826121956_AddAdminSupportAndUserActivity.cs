using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminSupportAndUserActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdminUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupportTicketId = table.Column<long>(type: "INTEGER", nullable: false),
                    SenderType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportMessages_SupportTickets_SupportTicketId",
                        column: x => x.SupportTicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserActivities",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    AppVersion = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    OsVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    DeviceModel = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivities", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserActivities_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_AdminUserId_CreatedAtUtc",
                table: "AdminAuditLogs",
                columns: new[] { "AdminUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_CreatedAtUtc",
                table: "AdminAuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SupportMessages_SupportTicketId_CreatedAtUtc",
                table: "SupportMessages",
                columns: new[] { "SupportTicketId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_LastSeenAtUtc",
                table: "UserActivities",
                column: "LastSeenAtUtc");

            migrationBuilder.Sql(
                """
                INSERT INTO UserActivities (UserId, RegisteredAtUtc, LastSeenAtUtc, Platform, AppVersion, OsVersion, DeviceModel)
                SELECT Id, NULL, NULL, NULL, NULL, NULL, NULL
                FROM AspNetUsers;

                INSERT INTO SupportMessages (SupportTicketId, SenderType, Message, CreatedAtUtc)
                SELECT Id, 'User', Message, CreatedAtUtc
                FROM SupportTickets;

                INSERT INTO SupportMessages (SupportTicketId, SenderType, Message, CreatedAtUtc)
                SELECT SupportTicketId, 'Support', Body, CreatedAtUtc
                FROM InboxMessages
                WHERE SupportTicketId IS NOT NULL AND Type = 'SupportReply';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs");

            migrationBuilder.DropTable(
                name: "SupportMessages");

            migrationBuilder.DropTable(
                name: "UserActivities");
        }
    }
}
