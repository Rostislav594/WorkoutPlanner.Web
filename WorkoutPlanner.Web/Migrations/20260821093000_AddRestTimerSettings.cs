using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkoutPlanner.Web.Data;

#nullable disable

namespace WorkoutPlanner.Web.Migrations;

[DbContext(typeof(WorkoutDbContext))]
[Migration("20260821093000_AddRestTimerSettings")]
public partial class AddRestTimerSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "RestBetweenExercisesSeconds",
            table: "UserProfiles",
            type: "INTEGER",
            nullable: false,
            defaultValue: 120);

        migrationBuilder.AddColumn<int>(
            name: "RestBetweenSetsSeconds",
            table: "UserProfiles",
            type: "INTEGER",
            nullable: false,
            defaultValue: 90);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RestBetweenExercisesSeconds", table: "UserProfiles");
        migrationBuilder.DropColumn(name: "RestBetweenSetsSeconds", table: "UserProfiles");
    }
}
