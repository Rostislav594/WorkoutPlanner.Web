using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutPlanner.Web.Migrations;

public partial class AddInboxPublications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InboxPublications",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Type = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                Preview = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                Body = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                ImagePath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                PublishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                SendPushNotification = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InboxPublications", x => x.Id);
                table.ForeignKey("FK_InboxPublications_AspNetUsers_CreatedByUserId", x => x.CreatedByUserId,
                    "AspNetUsers", "Id", onDelete: ReferentialAction.SetNull);
            });
        migrationBuilder.CreateTable(
            name: "InboxPublicationReads",
            columns: table => new
            {
                PublicationId = table.Column<long>(type: "INTEGER", nullable: false),
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                ReadAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InboxPublicationReads", x => new { x.PublicationId, x.UserId });
                table.ForeignKey("FK_InboxPublicationReads_AspNetUsers_UserId", x => x.UserId,
                    "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_InboxPublicationReads_InboxPublications_PublicationId", x => x.PublicationId,
                    "InboxPublications", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("IX_InboxPublicationReads_UserId", "InboxPublicationReads", "UserId");
        migrationBuilder.CreateIndex("IX_InboxPublications_CreatedByUserId", "InboxPublications", "CreatedByUserId");
        migrationBuilder.CreateIndex("IX_InboxPublications_PublishedAtUtc", "InboxPublications", "PublishedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("InboxPublicationReads");
        migrationBuilder.DropTable("InboxPublications");
    }
}
