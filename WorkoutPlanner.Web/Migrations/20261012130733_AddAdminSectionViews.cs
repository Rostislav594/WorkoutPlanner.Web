using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminSectionViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminSectionViews",
                columns: table => new
                {
                    AdminUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Section = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    LastViewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSectionViews", x => new { x.AdminUserId, x.Section });
                    table.ForeignKey(
                        name: "FK_AdminSectionViews_AspNetUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminSectionViews");
        }
    }
}
