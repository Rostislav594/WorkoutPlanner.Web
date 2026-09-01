using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services;
using WorkoutPlanner.Web.Tests.Infrastructure;
using ContractHistory = WorkoutPlanner.Web.Application.Contracts.WorkoutHistory;
using ContractHistoryDetails = WorkoutPlanner.Web.Application.Contracts.WorkoutHistoryDetails;
using ContractHistoryExercise = WorkoutPlanner.Web.Application.Contracts.WorkoutHistoryExercise;
using ContractHistorySet = WorkoutPlanner.Web.Application.Contracts.WorkoutHistorySet;
using ContractExerciseStatus = WorkoutPlanner.Web.Application.Contracts.ExerciseStatus;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class HistoryServiceTests
{
    [Fact]
    public async Task History_IsScopedToCurrentUser_AndPreservesSetSnapshot()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        await application.CreateUserAsync("user-b");
        application.AuthenticationStateProvider.SetUser("user-a");

        var details = JsonSerializer.Serialize(
            new ContractHistoryDetails
            {
                Exercises =
                [
                    new ContractHistoryExercise
                    {
                        Name = "Squat",
                        Status = ContractExerciseStatus.Hard,
                        Sets =
                        [
                            new ContractHistorySet
                            {
                                SetNumber = 1,
                                Weight = 80,
                                Repetitions = 8,
                                Completed = true
                            },
                            new ContractHistorySet
                            {
                                SetNumber = 2,
                                Weight = 85,
                                Repetitions = 6,
                                Completed = true
                            }
                        ]
                    }
                ]
            });

        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<WorkoutDbContext>();
            db.WorkoutHistory.AddRange(
                new WorkoutHistory
                {
                    UserId = "user-b",
                    WorkoutName = "User B history",
                    Date = DateTime.UtcNow,
                    Details = "{}"
                },
                new WorkoutHistory
                {
                    UserId = null,
                    WorkoutName = "Legacy history",
                    Date = DateTime.UtcNow,
                    Details = "{}"
                });
            await db.SaveChangesAsync();

            var service = scope.ServiceProvider
                .GetRequiredService<HistoryService>();
            await service.AddHistoryAsync(
                new ContractHistory
                {
                    WorkoutName = "User A history",
                    Date = DateTime.UtcNow,
                    Details = details
                });

            var visibleHistory = await service.GetHistoryAsync();

            var savedItem = Assert.Single(visibleHistory);
            var snapshot = JsonSerializer.Deserialize<ContractHistoryDetails>(
                savedItem.Details);
            Assert.NotNull(snapshot);
            Assert.Equal(
                [80d, 85d],
                snapshot.Exercises[0].Sets.Select(x => x.Weight).ToArray());
        }

        await using var verificationScope = application.CreateScope();
        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<WorkoutDbContext>();

        Assert.Equal(3, await verificationDb.WorkoutHistory.CountAsync());
        Assert.True(await verificationDb.WorkoutHistory.AnyAsync(x =>
            x.UserId == "user-a" && x.WorkoutName == "User A history"));
        Assert.True(await verificationDb.WorkoutHistory.AnyAsync(x =>
            x.UserId == "user-b"));
        Assert.True(await verificationDb.WorkoutHistory.AnyAsync(x =>
            x.UserId == null));
    }

    [Fact]
    public async Task DeleteHistory_RemovesMatchingProgress_KeepsCalendar_AndRebasesChart()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        await application.CreateUserAsync("user-b");
        application.AuthenticationStateProvider.SetUser("user-a");

        var firstDate = DateTime.Today.AddDays(-20).AddHours(18);
        var deletedDate = DateTime.Today.AddDays(-10).AddHours(18);
        var lastDate = DateTime.Today.AddDays(-1).AddHours(18);
        int historyId;

        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            var plan = new TrainingPlan
            {
                UserId = "user-a",
                WorkoutName = "Full body",
                Date = firstDate
            };
            db.TrainingPlans.Add(plan);
            await db.SaveChangesAsync();

            var history = new WorkoutHistory
            {
                UserId = "user-a",
                WorkoutName = "Full body",
                Date = deletedDate,
                Details = "{}"
            };
            db.WorkoutHistory.AddRange(
                new WorkoutHistory
                {
                    UserId = "user-a",
                    WorkoutName = "Full body",
                    Date = firstDate,
                    Details = "{}"
                },
                history,
                new WorkoutHistory
                {
                    UserId = "user-a",
                    WorkoutName = "Full body",
                    Date = lastDate,
                    Details = "{}"
                });
            db.ProgressSnapshots.AddRange(
                new ProgressSnapshot { UserId = "user-a", WorkoutName = "Full body", Date = firstDate, Score = 100 },
                new ProgressSnapshot { UserId = "user-a", WorkoutName = "Full body", Date = deletedDate, Score = 120 },
                new ProgressSnapshot { UserId = "user-a", WorkoutName = "Full body", Date = lastDate, Score = 150 },
                new ProgressSnapshot { UserId = "user-a", WorkoutName = "Other", Date = deletedDate, Score = 90 },
                new ProgressSnapshot { UserId = "user-b", WorkoutName = "Full body", Date = deletedDate, Score = 80 });
            db.ExerciseProgressSnapshots.AddRange(
                new ExerciseProgressSnapshot { UserId = "user-a", WorkoutName = "Full body", ExerciseName = "Squat", Date = firstDate, Score = 40 },
                new ExerciseProgressSnapshot { UserId = "user-a", WorkoutName = "Full body", ExerciseName = "Squat", Date = deletedDate, Score = 50 },
                new ExerciseProgressSnapshot { UserId = "user-a", WorkoutName = "Full body", ExerciseName = "Squat", Date = lastDate, Score = 60 },
                new ExerciseProgressSnapshot { UserId = "user-b", WorkoutName = "Full body", ExerciseName = "Squat", Date = deletedDate, Score = 70 });
            db.WorkoutDays.Add(new WorkoutDay
            {
                UserId = "user-a",
                TrainingPlanId = plan.Id,
                Date = deletedDate.Date,
                IsCompleted = true
            });
            await db.SaveChangesAsync();
            historyId = history.Id;

            var historyService = scope.ServiceProvider.GetRequiredService<HistoryService>();
            Assert.True(await historyService.DeleteHistoryAsync(historyId));
        }

        await using var verificationScope = application.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<WorkoutDbContext>();

        Assert.False(await verificationDb.WorkoutHistory.AnyAsync(x => x.Id == historyId));
        Assert.False(await verificationDb.ProgressSnapshots.AnyAsync(x =>
            x.UserId == "user-a" && x.WorkoutName == "Full body" && x.Date == deletedDate));
        Assert.False(await verificationDb.ExerciseProgressSnapshots.AnyAsync(x =>
            x.UserId == "user-a" && x.WorkoutName == "Full body" && x.Date == deletedDate));
        Assert.True(await verificationDb.WorkoutDays.AnyAsync(x =>
            x.UserId == "user-a" && x.Date == deletedDate.Date && x.IsCompleted));
        Assert.True(await verificationDb.ProgressSnapshots.AnyAsync(x =>
            x.UserId == "user-a" && x.WorkoutName == "Other" && x.Date == deletedDate));
        Assert.True(await verificationDb.ProgressSnapshots.AnyAsync(x =>
            x.UserId == "user-b" && x.WorkoutName == "Full body" && x.Date == deletedDate));
        Assert.True(await verificationDb.ExerciseProgressSnapshots.AnyAsync(x =>
            x.UserId == "user-b" && x.WorkoutName == "Full body" && x.Date == deletedDate));

        var progressService = verificationScope.ServiceProvider.GetRequiredService<ProgressService>();
        var workoutChart = await progressService.GetWorkoutChartAsync("Full body");
        var exerciseChart = await progressService.GetExerciseChartAsync("Full body", "Squat");

        Assert.Equal([0m, 50m], workoutChart.Select(x => x.Percent).ToArray());
        Assert.Equal([0m, 50m], exerciseChart.Select(x => x.Percent).ToArray());
    }
}
