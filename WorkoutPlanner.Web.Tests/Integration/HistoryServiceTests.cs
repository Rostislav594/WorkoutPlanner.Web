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
}
