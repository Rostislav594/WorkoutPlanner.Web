using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseReps",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "CurrentLevel",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "CurrentWeight",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "WeightStep",
                table: "Exercises");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseReps",
                table: "Exercises",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CurrentLevel",
                table: "Exercises",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "CurrentWeight",
                table: "Exercises",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "WeightStep",
                table: "Exercises",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
