using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutPlanner.Web.Migrations
{
    /// <summary>
    /// Отдых после каждого подхода шаблона отдельно. NULL — действует отдых
    /// упражнения по умолчанию, поэтому существующие подходы не меняются.
    /// </summary>
    public partial class AddSetRestTimers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RestAfterSeconds",
                table: "ExerciseTemplateSets",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RestAfterSeconds",
                table: "ExerciseTemplateSets");
        }
    }
}
