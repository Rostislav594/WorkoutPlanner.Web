using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Json;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;
using WorkoutPlanner.Web.Tests.Infrastructure;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class AccountDeletionServiceTests
{
    [Fact]
    public async Task DeleteCurrentAccountAsync_RemovesOwnedGraphAndPhotos_ButPreservesOtherUser()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        await application.CreateUserAsync("user-b");
        application.AuthenticationStateProvider.SetUser("user-a");

        var uploadDirectory = Path.Combine(
            application.ContentRootPath,
            "App_Data",
            "WorkoutImages");
        Directory.CreateDirectory(uploadDirectory);

        var userAPhoto = Path.Combine(uploadDirectory, "user-a.jpg");
        var userALegacyPhoto = Path.Combine(
            uploadDirectory,
            "user-a-legacy.jpg");
        var userBPhoto = Path.Combine(uploadDirectory, "user-b.jpg");
        await File.WriteAllBytesAsync(userAPhoto, [1, 2, 3]);
        await File.WriteAllBytesAsync(userALegacyPhoto, [4, 5, 6]);
        await File.WriteAllBytesAsync(userBPhoto, [7, 8, 9]);

        int userAPlanId;
        int userAExerciseId;
        int userATrainingSessionId;

        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<WorkoutDbContext>();
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<IdentityUser>>();
            var userA = await userManager.FindByIdAsync("user-a");
            Assert.NotNull(userA);
            var claimResult = await userManager.AddClaimAsync(
                userA,
                new Claim("app-guide", "completed"));
            Assert.True(claimResult.Succeeded);

            var muscle = new Muscle { Name = "Chest" };
            var definition = new ExerciseDefinition
            {
                Name = "Bench press",
                SearchName = "bench press",
                PrimaryMuscle = muscle,
                ExerciseCoefficient = 1,
                Type = ExerciseType.Compound
            };
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
            var userAExercise = new Exercise
            {
                UserId = "user-a",
                Name = definition.Name,
                WorkoutName = userAPlan.WorkoutName,
                TrainingPlan = userAPlan,
                ExerciseDefinition = definition,
                PhotoPath = "/WorkoutImages/user-a.jpg",
                Sets =
                [
                    new ExerciseTemplateSet
                    {
                        SetNumber = 1,
                        Weight = 50,
                        Repetitions = 8
                    }
                ]
            };
            var structurallyOwnedLegacyExercise = new Exercise
            {
                UserId = null,
                Name = "Legacy child",
                WorkoutName = userAPlan.WorkoutName,
                TrainingPlan = userAPlan,
                PhotoPath = "/WorkoutImages/user-a-legacy.jpg"
            };
            var userBExercise = new Exercise
            {
                UserId = "user-b",
                Name = "User B exercise",
                WorkoutName = userBPlan.WorkoutName,
                TrainingPlan = userBPlan,
                PhotoPath = "/WorkoutImages/user-b.jpg"
            };
            var userASession = new TrainingSession
            {
                UserId = "user-a",
                Date = DateTime.UtcNow,
                TrainingPlan = userAPlan,
                Exercises =
                [
                    new TrainingSessionExercise
                    {
                        ExerciseDefinition = definition,
                        Status = ExerciseStatus.Hard,
                        ExerciseIndex = 400,
                        Sets =
                        [
                            new ExerciseSet
                            {
                                SetNumber = 1,
                                Weight = 50,
                                Repetitions = 8,
                                Completed = true
                            }
                        ]
                    }
                ]
            };
            var historyDetails = JsonSerializer.Serialize(
                new WorkoutHistoryDetails
                {
                    Exercises =
                    [
                        new WorkoutHistoryExercise
                        {
                            Name = definition.Name,
                            Photos = ["/WorkoutImages/user-a.jpg"]
                        }
                    ]
                });

            db.Exercises.AddRange(
                userAExercise,
                structurallyOwnedLegacyExercise,
                userBExercise);
            db.TrainingSessions.Add(userASession);
            db.WorkoutDays.AddRange(
                new WorkoutDay
                {
                    UserId = "user-a",
                    Date = DateTime.Today,
                    TrainingPlanId = 0
                },
                new WorkoutDay
                {
                    UserId = null,
                    Date = DateTime.Today.AddDays(1),
                    TrainingPlanId = 0
                },
                new WorkoutDay
                {
                    UserId = "user-b",
                    Date = DateTime.Today,
                    TrainingPlanId = 0
                });
            db.WorkoutHistory.AddRange(
                new WorkoutHistory
                {
                    UserId = "user-a",
                    WorkoutName = userAPlan.WorkoutName,
                    Date = DateTime.UtcNow,
                    Details = historyDetails
                },
                new WorkoutHistory
                {
                    UserId = "user-b",
                    WorkoutName = userBPlan.WorkoutName,
                    Date = DateTime.UtcNow,
                    Details = "{}"
                });
            db.ProgressSnapshots.AddRange(
                new ProgressSnapshot
                {
                    UserId = "user-a",
                    WorkoutName = userAPlan.WorkoutName,
                    Date = DateTime.UtcNow,
                    Score = 1
                },
                new ProgressSnapshot
                {
                    UserId = "user-b",
                    WorkoutName = userBPlan.WorkoutName,
                    Date = DateTime.UtcNow,
                    Score = 2
                });
            db.ExerciseProgressSnapshots.AddRange(
                new ExerciseProgressSnapshot
                {
                    UserId = "user-a",
                    WorkoutName = userAPlan.WorkoutName,
                    ExerciseName = definition.Name,
                    Date = DateTime.UtcNow,
                    Score = 1
                },
                new ExerciseProgressSnapshot
                {
                    UserId = "user-b",
                    WorkoutName = userBPlan.WorkoutName,
                    ExerciseName = userBExercise.Name,
                    Date = DateTime.UtcNow,
                    Score = 2
                });
            db.UserProfiles.AddRange(
                CreateProfile("user-a"),
                CreateProfile("user-b"));

            await db.SaveChangesAsync();

            var days = await db.WorkoutDays.OrderBy(x => x.Id).ToListAsync();
            days[0].TrainingPlanId = userAPlan.Id;
            days[1].TrainingPlanId = userAPlan.Id;
            days[2].TrainingPlanId = userBPlan.Id;
            await db.SaveChangesAsync();

            userAPlanId = userAPlan.Id;
            userAExerciseId = userAExercise.Id;
            userATrainingSessionId = userASession.Id;

            var deletionService = scope.ServiceProvider
                .GetRequiredService<AccountDeletionService>();
            var result = await deletionService.DeleteCurrentAccountAsync();

            Assert.True(
                result.Succeeded,
                string.Join("; ", result.Errors.Select(x => x.Description)));
        }

        await using var verificationScope = application.CreateScope();
        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<WorkoutDbContext>();

        Assert.False(await verificationDb.Users.AnyAsync(x =>
            x.Id == "user-a"));
        Assert.True(await verificationDb.Users.AnyAsync(x =>
            x.Id == "user-b"));
        Assert.False(await verificationDb.Set<IdentityUserClaim<string>>()
            .AnyAsync(x => x.UserId == "user-a"));
        Assert.False(await verificationDb.TrainingPlans.AnyAsync(x =>
            x.Id == userAPlanId));
        Assert.True(await verificationDb.TrainingPlans.AnyAsync(x =>
            x.UserId == "user-b"));
        Assert.False(await verificationDb.Exercises.AnyAsync(x =>
            x.Id == userAExerciseId ||
            x.TrainingPlanId == userAPlanId));
        Assert.False(await verificationDb.ExerciseTemplateSets.AnyAsync(x =>
            x.ExerciseId == userAExerciseId));
        Assert.False(await verificationDb.TrainingSessions.AnyAsync(x =>
            x.Id == userATrainingSessionId));
        Assert.False(await verificationDb.WorkoutDays.AnyAsync(x =>
            x.TrainingPlanId == userAPlanId ||
            x.UserId == "user-a"));
        Assert.False(await verificationDb.WorkoutHistory.AnyAsync(x =>
            x.UserId == "user-a"));
        Assert.False(await verificationDb.ProgressSnapshots.AnyAsync(x =>
            x.UserId == "user-a"));
        Assert.False(await verificationDb.ExerciseProgressSnapshots.AnyAsync(x =>
            x.UserId == "user-a"));
        Assert.False(await verificationDb.UserProfiles.AnyAsync(x =>
            x.UserId == "user-a"));
        Assert.True(await verificationDb.WorkoutHistory.AnyAsync(x =>
            x.UserId == "user-b"));
        Assert.True(await verificationDb.ProgressSnapshots.AnyAsync(x =>
            x.UserId == "user-b"));
        Assert.True(await verificationDb.ExerciseProgressSnapshots.AnyAsync(x =>
            x.UserId == "user-b"));
        Assert.True(await verificationDb.UserProfiles.AnyAsync(x =>
            x.UserId == "user-b"));

        Assert.False(File.Exists(userAPhoto));
        Assert.False(File.Exists(userALegacyPhoto));
        Assert.True(File.Exists(userBPhoto));
    }

    private static UserProfile CreateProfile(string userId)
    {
        return new UserProfile
        {
            UserId = userId,
            FirstName = userId,
            LastName = "Test",
            Gender = "Not specified",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
