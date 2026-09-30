using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutPlanner.Web.Migrations
{
    /// <summary>
    /// Отдых после каждого упражнения шаблона отдельно. NULL — действует отдых
    /// между упражнениями шаблона, поэтому существующие упражнения не меняются.
    /// </summary>
    public partial class AddExerciseRestAfter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RestAfterExerciseSeconds",
                table: "Exercises",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RestAfterExerciseSeconds",
                table: "Exercises");
        }
    }
}
