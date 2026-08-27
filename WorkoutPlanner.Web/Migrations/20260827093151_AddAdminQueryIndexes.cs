using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WorkoutHistory_Date",
                table: "WorkoutHistory",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Status_UpdatedAtUtc",
                table: "SupportTickets",
                columns: new[] { "Status", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkoutHistory_Date",
                table: "WorkoutHistory");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_Status_UpdatedAtUtc",
                table: "SupportTickets");
        }
    }
}
