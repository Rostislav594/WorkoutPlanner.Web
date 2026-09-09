using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWearOsDevicePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "ExerciseTemplateSets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "WatchDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RefreshTokenHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    RefreshTokenExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AppVersion = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    DeviceModel = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchDevices", x => x.Id);
                    table.CheckConstraint("CK_WatchDevices_RefreshTokenExpiry", "[RefreshTokenExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_WatchDevices_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchPairingCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    CodeHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchPairingCodes", x => x.Id);
                    table.CheckConstraint("CK_WatchPairingCodes_AttemptCount", "[AttemptCount] >= 0");
                    table.CheckConstraint("CK_WatchPairingCodes_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_WatchPairingCodes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchSyncOperations",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WatchDeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchSyncOperations", x => x.OperationId);
                    table.CheckConstraint("CK_WatchSyncOperations_Expiry", "[ExpiresAtUtc] > [ReceivedAtUtc]");
                    table.ForeignKey(
                        name: "FK_WatchSyncOperations_WatchDevices_WatchDeviceId",
                        column: x => x.WatchDeviceId,
                        principalTable: "WatchDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchDevices_DeviceId",
                table: "WatchDevices",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchDevices_RefreshTokenExpiresAtUtc",
                table: "WatchDevices",
                column: "RefreshTokenExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WatchDevices_UserId_RevokedAtUtc",
                table: "WatchDevices",
                columns: new[] { "UserId", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchPairingCodes_CodeHash",
                table: "WatchPairingCodes",
                column: "CodeHash");

            migrationBuilder.CreateIndex(
                name: "IX_WatchPairingCodes_ExpiresAtUtc",
                table: "WatchPairingCodes",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WatchPairingCodes_UserId_UsedAtUtc_ExpiresAtUtc",
                table: "WatchPairingCodes",
                columns: new[] { "UserId", "UsedAtUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchSyncOperations_ExpiresAtUtc",
                table: "WatchSyncOperations",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WatchSyncOperations_WatchDeviceId_ReceivedAtUtc",
                table: "WatchSyncOperations",
                columns: new[] { "WatchDeviceId", "ReceivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchPairingCodes");

            migrationBuilder.DropTable(
                name: "WatchSyncOperations");

            migrationBuilder.DropTable(
                name: "WatchDevices");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ExerciseTemplateSets");
        }
    }
}
