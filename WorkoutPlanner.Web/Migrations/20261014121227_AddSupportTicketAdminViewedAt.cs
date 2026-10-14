using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportTicketAdminViewedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AdminViewedAtUtc",
                table: "SupportTickets",
                type: "TEXT",
                nullable: true);

            // Обращения, которые уже были до этой версии, админ видел — новыми они не становятся.
            migrationBuilder.Sql("""UPDATE "SupportTickets" SET "AdminViewedAtUtc" = "UpdatedAtUtc";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminViewedAtUtc",
                table: "SupportTickets");
        }
    }
}
