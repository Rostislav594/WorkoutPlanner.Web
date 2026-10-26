using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutPlanner.Web.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddSupportTicketKindPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Все прежние обращения — сообщения о проблемах.
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "SupportTickets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Problem");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "SupportTickets");
        }
    }
}
