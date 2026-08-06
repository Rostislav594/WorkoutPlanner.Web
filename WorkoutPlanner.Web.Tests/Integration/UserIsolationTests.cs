using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services;
using WorkoutPlanner.Web.Tests.Infrastructure;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class UserIsolationTests
{
    [Fact]
    public async Task Services_DoNotReadModifyOrScheduleAnotherUsersData()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        await application.CreateUserAsync("user-b");
        application.AuthenticationStateProvider.SetUser("user-a");

        int userBPlanId;

        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<WorkoutDbContext>();
            var userAPlan = new TrainingPlan
            {
                UserId = "user-a",
                WorkoutName = "User A plan",
                Date = DateTime.Today
            };
            var userBPlan = new TrainingPlan
            {
                UserId = "user-b",
                WorkoutName = "User B plan",
                Date = DateTime.Today
            };
            var legacyPlan = new TrainingPlan
            {
                UserId = null,
                WorkoutName = "Legacy plan",
                Date = DateTime.Today
            };

            db.TrainingPlans.AddRange(userAPlan, userBPlan, legacyPlan);
            await db.SaveChangesAsync();
            userBPlanId = userBPlan.Id;

            db.Exercises.AddRange(
                new Exercise
                {
                    UserId = "user-a",
                    Name = "User A exercise",
                    WorkoutName = userAPlan.WorkoutName,
                    TrainingPlanId = userAPlan.Id
                },
                new Exercise
                {
                    UserId = "user-b",
                    Name = "User B exercise",
                    WorkoutName = userAPlan.WorkoutName,
                    TrainingPlanId = userBPlan.Id
                },
                new Exercise
                {
                    UserId = null,
                    Name = "Legacy exercise",
                    WorkoutName = userAPlan.WorkoutName,
                    TrainingPlanId = legacyPlan.Id
                });
            db.ProgressSnapshots.AddRange(
                new ProgressSnapshot
                {
                    UserId = "user-a",
                    WorkoutName = "Shared progress",
                    Date = DateTime.UtcNow,
                    Score = 1
                },
                new ProgressSnapshot
                {
                    UserId = "user-b",
                    WorkoutName = "Shared progress",
                    Date = DateTime.UtcNow,
                    Score = 2
                },
                new ProgressSnapshot
                {
                    UserId = null,
                    WorkoutName = "Shared progress",
                    Date = DateTime.UtcNow,
                    Score = 3
                });
            await db.SaveChangesAsync();

            var planService = scope.ServiceProvider
                .GetRequiredService<TrainingPlanService>();
            var exerciseService = scope.ServiceProvider
                .GetRequiredService<ExerciseService>();
            var workoutDayService = scope.ServiceProvider
                .GetRequiredService<WorkoutDayService>();
            var progressService = scope.ServiceProvider
                .GetRequiredService<ProgressService>();

            var visiblePlans = await planService.GetTrainingPlansAsync();
            var visibleExercises = await exerciseService.GetExercisesAsync(
                userAPlan.WorkoutName);

            Assert.Single(visiblePlans);
            Assert.Equal("user-a", visiblePlans[0].UserId);
            Assert.Single(visibleExercises);
            Assert.Equal("user-a", visibleExercises[0].UserId);
            var visibleProgress = await progressService
                .GetWorkoutProgressAsync("Shared progress");
            Assert.Single(visibleProgress);
            Assert.Equal("user-a", visibleProgress[0].UserId);
            Assert.Null(await planService.GetByIdAsync(userBPlan.Id));

            var renameResult = await planService.RenameAsync(
                userBPlan.Id,
                "Compromised");
            Assert.False(renameResult.Succeeded);

            await planService.DeleteAsync(userBPlan.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                workoutDayService.SaveDayAsync(
                    DateTime.Today.AddDays(1),
                    userBPlan.Id));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                exerciseService.AddExerciseAsync(
                    new Exercise
                    {
                        UserId = "user-a",
                        Name = "Cross-user exercise",
                        WorkoutName = userBPlan.WorkoutName,
                        TrainingPlanId = userBPlan.Id
                    }));

            await progressService.ClearAllProgressAsync();
        }

        await using var verificationScope = application.CreateScope();
        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<WorkoutDbContext>();

        Assert.True(await verificationDb.TrainingPlans.AnyAsync(x =>
            x.Id == userBPlanId &&
            x.UserId == "user-b" &&
            x.WorkoutName == "User B plan"));
        Assert.False(await verificationDb.WorkoutDays.AnyAsync());
        Assert.False(await verificationDb.Exercises.AnyAsync(x =>
            x.Name == "Cross-user exercise"));
        Assert.True(await verificationDb.TrainingPlans.AnyAsync(x =>
            x.UserId == null &&
            x.WorkoutName == "Legacy plan"));
        Assert.False(await verificationDb.ProgressSnapshots.AnyAsync(x =>
            x.UserId == "user-a"));
        Assert.True(await verificationDb.ProgressSnapshots.AnyAsync(x =>
            x.UserId == "user-b"));
        Assert.True(await verificationDb.ProgressSnapshots.AnyAsync(x =>
            x.UserId == null));
    }
}
