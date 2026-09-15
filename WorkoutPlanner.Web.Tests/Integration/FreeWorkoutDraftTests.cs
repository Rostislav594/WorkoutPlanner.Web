using Microsoft.Extensions.DependencyInjection;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services;
using WorkoutPlanner.Web.Tests.Infrastructure;
using ExerciseContract = WorkoutPlanner.Web.Application.Contracts.Exercise;
using ExerciseEntity = WorkoutPlanner.Web.Models.Exercise;
using ExerciseTemplateSetContract = WorkoutPlanner.Web.Application.Contracts.ExerciseTemplateSet;
using ExerciseTemplateSetEntity = WorkoutPlanner.Web.Models.ExerciseTemplateSet;
using TrainingPlanEntity = WorkoutPlanner.Web.Models.TrainingPlan;
using WorkoutDayEntity = WorkoutPlanner.Web.Models.WorkoutDay;

namespace WorkoutPlanner.Web.Tests.Integration;

/// <summary>
/// Свободная тренировка как активная.
/// </summary>
/// <remarks>
/// Раньше свободная тренировка существовала только в памяти телефона, поэтому
/// часы её не видели. Теперь она живёт планом-черновиком и отдаётся как активная
/// тренировка тем же путём, что и запланированная.
/// </remarks>
public sealed class FreeWorkoutDraftTests
{
    [Fact]
    public async Task GetActiveWorkoutAsync_ReturnsFreeDraft_WhenNothingIsScheduled()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        application.AuthenticationStateProvider.SetUser("user-a");

        int draftPlanId;
        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            var draft = CreateDraft("user-a");
            db.TrainingPlans.Add(draft);
            await db.SaveChangesAsync();
            draftPlanId = draft.Id;
        }

        await using var operationScope = application.CreateScope();
        var service = operationScope.ServiceProvider
            .GetRequiredService<IActiveWorkoutService>();

        var workout = await service.GetActiveWorkoutAsync();

        Assert.NotNull(workout);
        Assert.Equal(draftPlanId, workout.TrainingPlanId);
        Assert.True(FreeWorkoutDraftId.IsDraft(workout.WorkoutId));
        Assert.Equal(draftPlanId, FreeWorkoutDraftId.ToPlanId(workout.WorkoutId));
        Assert.Equal("Свободная тренировка", workout.WorkoutName);
        Assert.Equal(["Жим лёжа"], workout.Exercises.Select(x => x.Name));
        Assert.Equal([1, 2], workout.Exercises[0].Sets.Select(x => x.SetNumber));
    }

    [Fact]
    public async Task GetActiveWorkoutAsync_PrefersScheduledWorkoutOverDraft()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        application.AuthenticationStateProvider.SetUser("user-a");

        int scheduledDayId;
        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            var planned = new TrainingPlanEntity
            {
                UserId = "user-a",
                WorkoutName = "По плану",
                Date = DateTime.Today,
                Exercises =
                [
                    new ExerciseEntity
                    {
                        UserId = "user-a",
                        Name = "Присед",
                        WorkoutName = "По плану",
                        Sets = [new ExerciseTemplateSetEntity { SetNumber = 1, Weight = 100, Repetitions = 5 }]
                    }
                ]
            };
            db.TrainingPlans.Add(planned);
            db.TrainingPlans.Add(CreateDraft("user-a"));
            await db.SaveChangesAsync();

            var day = new WorkoutDayEntity
            {
                UserId = "user-a",
                Date = DateTime.Today,
                TrainingPlanId = planned.Id
            };
            db.WorkoutDays.Add(day);
            await db.SaveChangesAsync();
            scheduledDayId = day.Id;
        }

        await using var operationScope = application.CreateScope();
        var workout = await operationScope.ServiceProvider
            .GetRequiredService<IActiveWorkoutService>()
            .GetActiveWorkoutAsync();

        // Запланированная тренировка важнее: черновик мог остаться от прошлого
        // раза, а расписание на сегодня человек задал сознательно.
        Assert.NotNull(workout);
        Assert.Equal(scheduledDayId, workout.WorkoutId);
        Assert.Equal("По плану", workout.WorkoutName);
    }

    [Fact]
    public async Task GetTrainingPlansAsync_HidesDraftFromTemplates()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        application.AuthenticationStateProvider.SetUser("user-a");

        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            db.TrainingPlans.Add(CreateDraft("user-a"));
            db.TrainingPlans.Add(new TrainingPlanEntity
            {
                UserId = "user-a",
                WorkoutName = "Обычный шаблон",
                Date = DateTime.Today
            });
            await db.SaveChangesAsync();
        }

        await using var operationScope = application.CreateScope();
        var plans = await operationScope.ServiceProvider
            .GetRequiredService<TrainingPlanService>()
            .GetTrainingPlansAsync();

        Assert.Equal(["Обычный шаблон"], plans.Select(x => x.WorkoutName));
    }

    [Fact]
    public async Task StartAsync_IsIdempotent_AndDiscardRemovesDraft()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        application.AuthenticationStateProvider.SetUser("user-a");

        await using var scope = application.CreateScope();
        var drafts = scope.ServiceProvider.GetRequiredService<IFreeWorkoutDraftService>();

        var first = await drafts.StartAsync();
        Assert.False(first.AlreadyStarted);
        Assert.True(first.TrainingPlanId > 0);
        Assert.Equal(FreeWorkoutDraftId.FromPlanId(first.TrainingPlanId), first.WorkoutId);

        // Повторный старт не создаёт вторую тренировку: запрос мог задвоиться
        // из-за обрыва связи.
        var second = await drafts.StartAsync();
        Assert.True(second.AlreadyStarted);
        Assert.Equal(first.TrainingPlanId, second.TrainingPlanId);

        Assert.NotNull(await drafts.GetAsync());
        Assert.True(await drafts.DiscardAsync());
        Assert.Null(await drafts.GetAsync());
        Assert.False(await drafts.DiscardAsync());
    }

    [Fact]
    public async Task StartedDraft_BecomesActiveWorkout_AndDisappearsAfterCompletion()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        application.AuthenticationStateProvider.SetUser("user-a");

        await using var scope = application.CreateScope();
        var drafts = scope.ServiceProvider.GetRequiredService<IFreeWorkoutDraftService>();
        var active = scope.ServiceProvider.GetRequiredService<IActiveWorkoutService>();

        var draft = await drafts.StartAsync();
        var started = await active.GetActiveWorkoutAsync();
        Assert.NotNull(started);
        Assert.Equal(draft.WorkoutId, started.WorkoutId);

        var definitionId = await SeedExerciseDefinitionAsync(application);
        var completion = scope.ServiceProvider
            .GetRequiredService<WorkoutCompletionService>();
        var result = await completion.CompleteFreeAsync(new FreeWorkoutCompletion
        {
            SaveAsTemplate = false,
            Exercises =
            [
                new ExerciseContract
                {
                    Name = "Жим лёжа",
                    WorkoutName = "Свободная тренировка",
                    ExerciseDefinitionId = definitionId,
                    Status = WorkoutPlanner.Web.Application.Contracts.ExerciseStatus.Medium,
                    Sets =
                    [
                        new ExerciseTemplateSetContract
                        {
                            SetNumber = 1,
                            Weight = 80,
                            Repetitions = 8,
                            Completed = true
                        }
                    ]
                }
            ]
        });

        Assert.True(result.Succeeded);
        // История записана, а черновик исчез — тренировка больше не идёт.
        Assert.Null(await drafts.GetAsync());
        Assert.Null(await active.GetActiveWorkoutAsync());
    }

    private static async Task<int> SeedExerciseDefinitionAsync(TestApplication application)
    {
        await using var scope = application.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        // Упражнение требует мышцы и коэффициента: без них падает внешний ключ,
        // а расчёт нагрузки не имеет смысла.
        var definition = new WorkoutPlanner.Web.Models.ExerciseDefinition
        {
            Name = "Жим лёжа",
            SearchName = "жим лёжа",
            PrimaryMuscle = new WorkoutPlanner.Web.Models.Muscle { Name = "Грудь" },
            ExerciseCoefficient = 1,
            Type = WorkoutPlanner.Web.Models.ExerciseType.Compound
        };
        db.ExerciseDefinitions.Add(definition);
        await db.SaveChangesAsync();
        return definition.Id;
    }

    private static TrainingPlanEntity CreateDraft(string userId) => new()
    {
        UserId = userId,
        WorkoutName = "Свободная тренировка",
        Date = DateTime.Today,
        IsFreeDraft = true,
        Exercises =
        [
            new ExerciseEntity
            {
                UserId = userId,
                Name = "Жим лёжа",
                WorkoutName = "Свободная тренировка",
                Sets =
                [
                    new ExerciseTemplateSetEntity { SetNumber = 1, Weight = 80, Repetitions = 8 },
                    new ExerciseTemplateSetEntity { SetNumber = 2, Weight = 80, Repetitions = 8 }
                ]
            }
        ]
    };
}
