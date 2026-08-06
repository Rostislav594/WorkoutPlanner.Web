using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkoutPlanner.Web.Data;

#nullable disable

namespace WorkoutPlanner.Web.Migrations;

[DbContext(typeof(WorkoutDbContext))]
[Migration("20260806130000_PersonalizeStarterPlans")]
public partial class PersonalizeStarterPlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO TrainingPlans (UserId, WorkoutName, Date)
            SELECT users.Id, starter.WorkoutName, CURRENT_TIMESTAMP
            FROM AspNetUsers AS users
            CROSS JOIN (
                SELECT 'Верх 1' AS WorkoutName
                UNION ALL SELECT 'Низ 1'
                UNION ALL SELECT 'Верх 2'
                UNION ALL SELECT 'Низ 2'
            ) AS starter
            WHERE NOT EXISTS (
                SELECT 1
                FROM TrainingPlans AS plans
                WHERE plans.UserId = users.Id
                  AND plans.WorkoutName = starter.WorkoutName
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately preserve user-owned plans on rollback.
    }
}
