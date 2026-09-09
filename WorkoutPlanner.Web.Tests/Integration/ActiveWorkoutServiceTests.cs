using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Tests.Infrastructure;
using ExerciseEntity = WorkoutPlanner.Web.Models.Exercise;
using ExerciseTemplateSetEntity = WorkoutPlanner.Web.Models.ExerciseTemplateSet;
using TrainingPlanEntity = WorkoutPlanner.Web.Models.TrainingPlan;
using WorkoutDayEntity = WorkoutPlanner.Web.Models.WorkoutDay;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class ActiveWorkoutServiceTests
{
    [Fact]
    public async Task GetActiveWorkoutAsync_ReturnsOwnedWorkoutInDeterministicOrder()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        application.AuthenticationStateProvider.SetUser("user-a");

        int expectedDayId;
        int firstExerciseId;
        int secondExerciseId;
        int firstSetId;

        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            var plan = new TrainingPlanEntity
            {
                UserId = "user-a",
                WorkoutName = "Full body",
                Date = DateTime.Today,
                Exercises =
                [
                    new ExerciseEntity
                    {
                        UserId = "user-a",
                        Name = "First",
                        WorkoutName = "Full body",
                        Sets =
                        [
                            new ExerciseTemplateSetEntity
                            {
                                SetNumber = 2,
                                Weight = 65,
                                Repetitions = 6
                            },
                            new ExerciseTemplateSetEntity
                            {
                                SetNumber = 1,
                                Weight = 60,
                                Repetitions = 8
                            }
                        ]
                    },
                    new ExerciseEntity
                    {
                        UserId = "user-a",
                        Name = "Second",
                        WorkoutName = "Full body",
                        Sets =
                        [
                            new ExerciseTemplateSetEntity
                            {
                                SetNumber = 1,
                                Weight = 20,
                                Repetitions = 10
                            }
                        ]
                    }
                ]
            };
            db.TrainingPlans.Add(plan);
            await db.SaveChangesAsync();

            var day = new WorkoutDayEntity
            {
                UserId = "user-a",
                Date = DateTime.Today,
                TrainingPlanId = plan.Id
            };
            db.WorkoutDays.Add(day);
            await db.SaveChangesAsync();

            expectedDayId = day.Id;
            firstExerciseId = plan.Exercises[0].Id;
            secondExerciseId = plan.Exercises[1].Id;
            firstSetId = plan.Exercises[0].Sets.Single(x => x.SetNumber == 1).Id;
        }

        await using var operationScope = application.CreateScope();
        var service = operationScope.ServiceProvider
            .GetRequiredService<IActiveWorkoutService>();

        var workout = await service.GetActiveWorkoutAsync();

        Assert.NotNull(workout);
        Assert.Equal(expectedDayId, workout.WorkoutId);
        Assert.Equal([firstExerciseId, secondExerciseId],
            workout.Exercises.Select(x => x.ExerciseId));
        Assert.Equal([0, 1], workout.Exercises.Select(x => x.Order));
        Assert.Equal([1, 2], workout.Exercises[0].Sets.Select(x => x.SetNumber));
        Assert.Equal(firstSetId, workout.Exercises[0].Sets[0].SetId);
        Assert.Equal([60d, 65d], workout.Exercises[0].Sets.Select(x => x.Weight));
    }

    [Fact]
    public async Task UpdateSetAsync_UpdatesOnlyOwnedActiveWorkoutSet()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        await application.CreateUserAsync("user-b");

        int ownedSetId;
        int otherSetId;
        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            (var ownedPlan, ownedSetId) = CreatePlan("user-a", "Owned", 40);
            (var otherPlan, otherSetId) = CreatePlan("user-b", "Other", 90);
            db.TrainingPlans.AddRange(ownedPlan, otherPlan);
            await db.SaveChangesAsync();
            db.WorkoutDays.AddRange(
                new WorkoutDayEntity
                {
                    UserId = "user-a",
                    Date = DateTime.Today,
                    TrainingPlanId = ownedPlan.Id
                },
                new WorkoutDayEntity
                {
                    UserId = "user-b",
                    Date = DateTime.Today,
                    TrainingPlanId = otherPlan.Id
                });
            await db.SaveChangesAsync();

            ownedSetId = ownedPlan.Exercises[0].Sets[0].Id;
            otherSetId = otherPlan.Exercises[0].Sets[0].Id;
        }

        application.AuthenticationStateProvider.SetUser("user-a");
        await using (var scope = application.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IActiveWorkoutService>();

            var updated = await service.UpdateSetAsync(
                ownedSetId,
                new UpdateWorkoutSet(42.5, 7, true, 0));
            var rejected = await service.UpdateSetAsync(
                otherSetId,
                new UpdateWorkoutSet(1, 1, true, 0));

            Assert.True(updated.Succeeded);
            Assert.Equal(42.5, updated.Set!.Weight);
            Assert.Equal(7, updated.Set.Repetitions);
            Assert.True(updated.Set.Completed);
            Assert.False(rejected.Succeeded);
            Assert.Equal(
                WorkoutSetUpdateFailure.ActiveWorkoutOrSetNotFound,
                rejected.Failure);
        }

        await using var verificationScope = application.CreateScope();
        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<WorkoutDbContext>();
        var owned = await verificationDb.ExerciseTemplateSets
            .AsNoTracking()
            .SingleAsync(x => x.Id == ownedSetId);
        var other = await verificationDb.ExerciseTemplateSets
            .AsNoTracking()
            .SingleAsync(x => x.Id == otherSetId);

        Assert.Equal(42.5, owned.Weight);
        Assert.Equal(7, owned.Repetitions);
        Assert.True(owned.Completed);
        Assert.Equal(90, other.Weight);
        Assert.False(other.Completed);
    }

    [Fact]
    public async Task UpdateSetAsync_RejectsSetWhenWorkoutIsNotActive()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        application.AuthenticationStateProvider.SetUser("user-a");

        int setId;
        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            (var plan, _) = CreatePlan("user-a", "Finished", 50);
            db.TrainingPlans.Add(plan);
            await db.SaveChangesAsync();
            db.WorkoutDays.Add(new WorkoutDayEntity
            {
                UserId = "user-a",
                Date = DateTime.Today,
                TrainingPlanId = plan.Id,
                IsCompleted = true
            });
            await db.SaveChangesAsync();
            setId = plan.Exercises[0].Sets[0].Id;
        }

        await using var operationScope = application.CreateScope();
        var service = operationScope.ServiceProvider
            .GetRequiredService<IActiveWorkoutService>();
        var result = await service.UpdateSetAsync(
            setId,
            new UpdateWorkoutSet(55, 8, true, 0));

        Assert.False(result.Succeeded);
        Assert.Equal(
            WorkoutSetUpdateFailure.ActiveWorkoutOrSetNotFound,
            result.Failure);
    }

    [Fact]
    public async Task UpdateSetAsync_ReturnsAuthoritativeStateForStaleVersion()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        application.AuthenticationStateProvider.SetUser("user-a");

        int setId;
        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            (var plan, _) = CreatePlan("user-a", "Versioned", 50);
            db.TrainingPlans.Add(plan);
            await db.SaveChangesAsync();
            db.WorkoutDays.Add(new WorkoutDayEntity
            {
                UserId = "user-a",
                Date = DateTime.Today,
                TrainingPlanId = plan.Id
            });
            await db.SaveChangesAsync();
            setId = plan.Exercises[0].Sets[0].Id;
        }

        await using var operationScope = application.CreateScope();
        var service = operationScope.ServiceProvider
            .GetRequiredService<IActiveWorkoutService>();
        var first = await service.UpdateSetAsync(
            setId,
            new UpdateWorkoutSet(50, 8, true, 0));
        var stale = await service.UpdateSetAsync(
            setId,
            new UpdateWorkoutSet(60, 6, false, 0));

        Assert.True(first.Succeeded);
        Assert.Equal(1, first.Set!.Version);
        Assert.False(stale.Succeeded);
        Assert.Equal(WorkoutSetUpdateFailure.Conflict, stale.Failure);
        Assert.NotNull(stale.Set);
        Assert.Equal(50, stale.Set.Weight);
        Assert.Equal(8, stale.Set.Repetitions);
        Assert.True(stale.Set.Completed);
        Assert.Equal(1, stale.Set.Version);
    }

    private static (TrainingPlanEntity Plan, int SetId) CreatePlan(
        string userId,
        string name,
        double weight)
    {
        var set = new ExerciseTemplateSetEntity
        {
            SetNumber = 1,
            Weight = weight,
            Repetitions = 8
        };
        var plan = new TrainingPlanEntity
        {
            UserId = userId,
            WorkoutName = name,
            Date = DateTime.Today,
            Exercises =
            [
                new ExerciseEntity
                {
                    UserId = userId,
                    Name = "Exercise",
                    WorkoutName = name,
                    Sets = [set]
                }
            ]
        };

        return (plan, set.Id);
    }
}
