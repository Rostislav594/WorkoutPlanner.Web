using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services;
using WorkoutPlanner.Web.Tests.Infrastructure;
using ContractExercise = WorkoutPlanner.Web.Application.Contracts.Exercise;
using ContractSet = WorkoutPlanner.Web.Application.Contracts.ExerciseTemplateSet;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class ExerciseSetWeightTests
{
    [Fact]
    public async Task AddExerciseAsync_PersistsIndividualWeightForEverySet()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        application.AuthenticationStateProvider.SetUser("user-a");

        int exerciseId;

        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<WorkoutDbContext>();
            var plan = new TrainingPlan
            {
                UserId = "user-a",
                WorkoutName = "Strength",
                Date = DateTime.Today
            };
            db.TrainingPlans.Add(plan);
            await db.SaveChangesAsync();

            var exercise = new ContractExercise
            {
                Name = "Bench press",
                WorkoutName = plan.WorkoutName,
                TrainingPlanId = plan.Id,
                SetsCount = 3,
                Sets =
                [
                    new ContractSet
                    {
                        SetNumber = 1,
                        Weight = 60,
                        Repetitions = 8
                    },
                    new ContractSet
                    {
                        SetNumber = 2,
                        Weight = 65,
                        Repetitions = 7
                    },
                    new ContractSet
                    {
                        SetNumber = 3,
                        Weight = 70,
                        Repetitions = 6
                    }
                ]
            };

            var service = scope.ServiceProvider
                .GetRequiredService<ExerciseService>();
            await service.AddExerciseAsync(exercise);
            exerciseId = exercise.Id;
        }

        await using var verificationScope = application.CreateScope();
        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<WorkoutDbContext>();
        var savedExercise = await verificationDb.Exercises
            .AsNoTracking()
            .Include(x => x.Sets)
            .SingleAsync(x => x.Id == exerciseId);

        Assert.Equal("user-a", savedExercise.UserId);
        Assert.Equal(
            [60d, 65d, 70d],
            savedExercise.Sets
                .OrderBy(x => x.SetNumber)
                .Select(x => x.Weight)
                .ToArray());
        Assert.Equal(
            [8, 7, 6],
            savedExercise.Sets
                .OrderBy(x => x.SetNumber)
                .Select(x => x.Repetitions)
                .ToArray());
    }
}
