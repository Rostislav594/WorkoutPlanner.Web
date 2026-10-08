using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutPlanner.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseLibraryTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BodyPart",
                table: "Muscles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Muscles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameUk",
                table: "Muscles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "ExerciseDefinitions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameUk",
                table: "ExerciseDefinitions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyPart",
                table: "Muscles");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Muscles");

            migrationBuilder.DropColumn(
                name: "NameUk",
                table: "Muscles");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "ExerciseDefinitions");

            migrationBuilder.DropColumn(
                name: "NameUk",
                table: "ExerciseDefinitions");
        }
    }
}
