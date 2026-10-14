using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddUserActivityDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserActivityDays",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivityDays", x => new { x.Day, x.UserId });
                    table.ForeignKey(
                        name: "FK_UserActivityDays_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityDays_UserId",
                table: "UserActivityDays",
                column: "UserId");

            // Прошлые дни восстанавливаем из того, что уже известно точно:
            // день регистрации, день последнего захода и дни завершённых тренировок.
            // Полной истории заходов раньше не было, поэтому прошлое неполное, но не выдуманное.
            // Время активности хранится в UTC — переводим в местное время сервера;
            // дата тренировки уже записана в местном времени.
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO "UserActivityDays" ("Day", "UserId")
                SELECT date(substr("RegisteredAtUtc", 1, 19), 'localtime'), "UserId"
                FROM "UserActivities"
                WHERE "RegisteredAtUtc" IS NOT NULL
                  AND "UserId" IN (SELECT "Id" FROM "AspNetUsers");

                INSERT OR IGNORE INTO "UserActivityDays" ("Day", "UserId")
                SELECT date(substr("LastSeenAtUtc", 1, 19), 'localtime'), "UserId"
                FROM "UserActivities"
                WHERE "LastSeenAtUtc" IS NOT NULL
                  AND "UserId" IN (SELECT "Id" FROM "AspNetUsers");

                INSERT OR IGNORE INTO "UserActivityDays" ("Day", "UserId")
                SELECT DISTINCT substr("Date", 1, 10), "UserId"
                FROM "WorkoutHistory"
                WHERE "UserId" IS NOT NULL
                  AND "UserId" IN (SELECT "Id" FROM "AspNetUsers");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserActivityDays");
        }
    }
}
