using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutPlanner.Web.Migrations
{
    /// <summary>
    /// Таймеры отдыха переезжают из профиля в шаблоны: отдых между подходами —
    /// в каждое упражнение, отдых между упражнениями — в сам шаблон.
    /// </summary>
    /// <remarks>
    /// Чтобы у человека ничего не поменялось, существующие упражнения и шаблоны
    /// получают значения, которые он выставил в профиле; у кого профиля нет —
    /// прежние значения по умолчанию.
    /// </remarks>
    public partial class AddRestTimersToTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RestBetweenExercisesSeconds",
                table: "TrainingPlans",
                type: "INTEGER",
                nullable: false,
                defaultValue: 120);

            migrationBuilder.AddColumn<int>(
                name: "RestBetweenSetsSeconds",
                table: "Exercises",
                type: "INTEGER",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.Sql(
                """
                UPDATE "Exercises"
                SET "RestBetweenSetsSeconds" = COALESCE(
                    (SELECT p."RestBetweenSetsSeconds"
                     FROM "UserProfiles" AS p
                     WHERE p."UserId" = "Exercises"."UserId"),
                    90);
                """);

            migrationBuilder.Sql(
                """
                UPDATE "TrainingPlans"
                SET "RestBetweenExercisesSeconds" = COALESCE(
                    (SELECT p."RestBetweenExercisesSeconds"
                     FROM "UserProfiles" AS p
                     WHERE p."UserId" = "TrainingPlans"."UserId"),
                    120);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RestBetweenExercisesSeconds",
                table: "TrainingPlans");

            migrationBuilder.DropColumn(
                name: "RestBetweenSetsSeconds",
                table: "Exercises");
        }
    }
}
