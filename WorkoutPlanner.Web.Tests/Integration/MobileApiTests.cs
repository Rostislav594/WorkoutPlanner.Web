using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Data;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class MobileApiTests
{
    [Fact]
    public async Task OpenApiDocument_IsAvailableOnlyWhenDevelopmentIsConfigured()
    {
        using var factory = new GymPlannerApiFactory("Development");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/auth/login", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/profile", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/training-plans", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/exercises/{id}", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/calendar", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/history", document, StringComparison.Ordinal);
        Assert.Contains(
            "/api/v1/progress/workouts/{trainingPlanId}",
            document,
            StringComparison.Ordinal);
        Assert.Contains("/api/v1/onboarding", document, StringComparison.Ordinal);
        Assert.Contains(
            "/api/v1/exercises/{exerciseId}/photo",
            document,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistrationLoginAndProfile_UseProtectedUserScopedApi()
    {
        using var factory = new GymPlannerApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var anonymousProfile = await client.GetAsync("/api/v1/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousProfile.StatusCode);
        Assert.Equal(
            "application/problem+json",
            anonymousProfile.Content.Headers.ContentType?.MediaType);

        using var webProfile = await client.GetAsync("/profile");
        Assert.Equal(HttpStatusCode.Redirect, webProfile.StatusCode);
        Assert.Equal(
            "/Account/Login",
            webProfile.Headers.Location?.AbsolutePath,
            ignoreCase: true);

        using var registration = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("mobile-a@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var userId = await db.Users
                .Where(x => x.Email == "mobile-a@example.test")
                .Select(x => x.Id)
                .SingleAsync();
            Assert.Equal(
                4,
                await db.TrainingPlans.CountAsync(x => x.UserId == userId));
            Assert.False(await db.TrainingPlans.AnyAsync(x => x.UserId == null));
        }

        using var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new MobileLoginRequest("mobile-a@example.test", "password1"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.True(tokens.ExpiresIn > 0);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var emptyProfile = await client.GetFromJsonAsync<ProfileResponse>(
            "/api/v1/profile");
        Assert.NotNull(emptyProfile);
        Assert.Equal("mobile-a@example.test", emptyProfile.Email);
        Assert.False(emptyProfile.HasProfile);

        using var update = await client.PutAsJsonAsync(
            "/api/v1/profile",
            new UpdateProfileRequest(
                "Mobile",
                "User",
                new DateTime(1990, 1, 2),
                "Not specified"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var savedProfile = await update.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.NotNull(savedProfile);
        Assert.True(savedProfile.HasProfile);
        Assert.Equal("Mobile", savedProfile.FirstName);

        using var refresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new MobileRefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshedTokens = await refresh.Content
            .ReadFromJsonAsync<AccessTokenResponse>();
        Assert.NotNull(refreshedTokens);
        Assert.False(string.IsNullOrWhiteSpace(refreshedTokens.AccessToken));

        using var secondClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var secondRegistration = await secondClient.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("mobile-b@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, secondRegistration.StatusCode);
        using var secondLogin = await secondClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new MobileLoginRequest("mobile-b@example.test", "password1"));
        var secondTokens = await secondLogin.Content
            .ReadFromJsonAsync<AccessTokenResponse>();
        Assert.NotNull(secondTokens);
        secondClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", secondTokens.AccessToken);
        var secondProfile = await secondClient.GetFromJsonAsync<ProfileResponse>(
            "/api/v1/profile");
        Assert.NotNull(secondProfile);
        Assert.Equal("mobile-b@example.test", secondProfile.Email);
        Assert.False(secondProfile.HasProfile);

        var firstProfileAgain = await client.GetFromJsonAsync<ProfileResponse>(
            "/api/v1/profile");
        Assert.NotNull(firstProfileAgain);
        Assert.Equal("Mobile", firstProfileAgain.FirstName);
    }

    [Fact]
    public async Task ExercisePhotoApi_ValidatesFiles_AndEnforcesOwnership()
    {
        using var factory = new GymPlannerApiFactory();
        using var firstUser = CreateClient(factory);
        using var secondUser = CreateClient(factory);
        using var firstRegistration = await firstUser.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("photo-a@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, firstRegistration.StatusCode);
        using var secondRegistration = await secondUser.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("photo-b@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, secondRegistration.StatusCode);
        SetBearer(
            firstUser,
            (await LoginAsync(
                firstUser,
                "photo-a@example.test",
                "password1",
                "Photo A")).AccessToken);
        SetBearer(
            secondUser,
            (await LoginAsync(
                secondUser,
                "photo-b@example.test",
                "password1",
                "Photo B")).AccessToken);

        using var createPlan = await firstUser.PostAsJsonAsync(
            "/api/v1/training-plans",
            new CreateTrainingPlanRequest("Photo plan"));
        var plan = await createPlan.Content
            .ReadFromJsonAsync<TrainingPlanApiResponse>();
        Assert.NotNull(plan);
        using var createExercise = await firstUser.PostAsJsonAsync(
            $"/api/v1/training-plans/{plan.Id}/exercises",
            new SaveExerciseRequest(
                "Photo exercise",
                1,
                "NotCompleted",
                null,
                [new SaveExerciseSetRequest(1, 8, 20, false)]));
        var exercise = await createExercise.Content
            .ReadFromJsonAsync<ExerciseApiResponse>();
        Assert.NotNull(exercise);

        using (var invalidContent = CreatePhotoContent(
                   [0x01, 0x02, 0x03, 0x04],
                   "image/png",
                   "../../unsafe.png"))
        using (var invalidUpload = await firstUser.PostAsync(
                   $"/api/v1/exercises/{exercise.Id}/photo",
                   invalidContent))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalidUpload.StatusCode);
        }

        using (var oversizedContent = CreatePhotoContent(
                   new byte[WorkoutPlanner.Web.Services.ExercisePhotoService.MaxPhotoSize + 1],
                   "image/png",
                   "oversized.png"))
        using (var oversizedUpload = await firstUser.PostAsync(
                   $"/api/v1/exercises/{exercise.Id}/photo",
                   oversizedContent))
        {
            Assert.Equal(
                HttpStatusCode.RequestEntityTooLarge,
                oversizedUpload.StatusCode);
        }

        var png = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x00
        };
        using var validContent = CreatePhotoContent(
            png,
            "image/png",
            "../../client-name.png");
        using var upload = await firstUser.PostAsync(
            $"/api/v1/exercises/{exercise.Id}/photo",
            validContent);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var uploadResponse = await upload.Content
            .ReadFromJsonAsync<ExercisePhotoApiResponse>();
        Assert.NotNull(uploadResponse);
        Assert.Equal(
            $"/api/v1/exercises/{exercise.Id}/photo",
            uploadResponse.DownloadUrl);

        string firstPhotoPath;
        string contentRootPath;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            firstPhotoPath = await db.Exercises
                .Where(x => x.Id == exercise.Id)
                .Select(x => x.PhotoPath!)
                .SingleAsync();
            contentRootPath = scope.ServiceProvider
                .GetRequiredService<IWebHostEnvironment>()
                .ContentRootPath;
        }
        Assert.StartsWith("/WorkoutImages/", firstPhotoPath, StringComparison.Ordinal);
        Assert.DoesNotContain("client-name", firstPhotoPath, StringComparison.Ordinal);
        var firstFile = Path.Combine(
            contentRootPath,
            "App_Data",
            "WorkoutImages",
            Path.GetFileName(firstPhotoPath));
        Assert.True(File.Exists(firstFile));

        using var download = await firstUser.GetAsync(
            $"/api/v1/exercises/{exercise.Id}/photo");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(png, await download.Content.ReadAsByteArrayAsync());

        using var foreignDownload = await secondUser.GetAsync(
            $"/api/v1/exercises/{exercise.Id}/photo");
        Assert.Equal(HttpStatusCode.NotFound, foreignDownload.StatusCode);
        using var foreignDelete = await secondUser.DeleteAsync(
            $"/api/v1/exercises/{exercise.Id}/photo");
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);
        using var foreignContent = CreatePhotoContent(
            png,
            "image/png",
            "foreign.png");
        using var foreignUpload = await secondUser.PostAsync(
            $"/api/v1/exercises/{exercise.Id}/photo",
            foreignContent);
        Assert.Equal(HttpStatusCode.NotFound, foreignUpload.StatusCode);

        var jpeg = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE0,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };
        using var replacementContent = CreatePhotoContent(
            jpeg,
            "image/jpeg",
            "replacement.jpg");
        using var replacement = await firstUser.PostAsync(
            $"/api/v1/exercises/{exercise.Id}/photo",
            replacementContent);
        Assert.Equal(HttpStatusCode.OK, replacement.StatusCode);
        Assert.False(File.Exists(firstFile));

        string replacementFile;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var replacementPath = await db.Exercises
                .Where(x => x.Id == exercise.Id)
                .Select(x => x.PhotoPath!)
                .SingleAsync();
            replacementFile = Path.Combine(
                contentRootPath,
                "App_Data",
                "WorkoutImages",
                Path.GetFileName(replacementPath));
        }
        Assert.True(File.Exists(replacementFile));

        using var delete = await firstUser.DeleteAsync(
            $"/api/v1/exercises/{exercise.Id}/photo");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.False(File.Exists(replacementFile));
        using var deletedDownload = await firstUser.GetAsync(
            $"/api/v1/exercises/{exercise.Id}/photo");
        Assert.Equal(HttpStatusCode.NotFound, deletedDownload.StatusCode);
    }

    [Fact]
    public async Task OnboardingApi_PersistsValidatedState_PerAuthenticatedUser()
    {
        using var factory = new GymPlannerApiFactory();
        using var firstUser = CreateClient(factory);
        using var secondUser = CreateClient(factory);
        using var anonymousState = await firstUser.GetAsync("/api/v1/onboarding");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousState.StatusCode);

        using var firstRegistration = await firstUser.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("guide-a@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, firstRegistration.StatusCode);
        using var secondRegistration = await secondUser.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("guide-b@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, secondRegistration.StatusCode);
        SetBearer(
            firstUser,
            (await LoginAsync(
                firstUser,
                "guide-a@example.test",
                "password1",
                "Guide A")).AccessToken);
        SetBearer(
            secondUser,
            (await LoginAsync(
                secondUser,
                "guide-b@example.test",
                "password1",
                "Guide B")).AccessToken);

        var initial = await firstUser.GetFromJsonAsync<OnboardingStateApiResponse>(
            "/api/v1/onboarding");
        Assert.NotNull(initial);
        Assert.False(initial.IsCompleted);
        Assert.Null(initial.StepId);

        using var invalidStep = await firstUser.PutAsJsonAsync(
            "/api/v1/onboarding/progress",
            new SaveOnboardingProgressRequest("client-invented-step", null));
        Assert.Equal(HttpStatusCode.BadRequest, invalidStep.StatusCode);

        using var saveProgress = await firstUser.PutAsJsonAsync(
            "/api/v1/onboarding/progress",
            new SaveOnboardingProgressRequest(
                "navigation-calendar-overview",
                "guided"));
        Assert.Equal(HttpStatusCode.OK, saveProgress.StatusCode);
        var saved = await saveProgress.Content
            .ReadFromJsonAsync<OnboardingStateApiResponse>();
        Assert.NotNull(saved);
        Assert.False(saved.IsCompleted);
        Assert.Equal("navigation-calendar-overview", saved.StepId);
        Assert.Equal("guided", saved.Outcome);
        Assert.NotNull(saved.UpdatedAtUtc);

        var secondState = await secondUser.GetFromJsonAsync<
            OnboardingStateApiResponse>("/api/v1/onboarding");
        Assert.NotNull(secondState);
        Assert.False(secondState.IsCompleted);
        Assert.Null(secondState.StepId);

        using var complete = await firstUser.PostAsJsonAsync(
            "/api/v1/onboarding/complete",
            new CompleteOnboardingRequest(null));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completed = await complete.Content
            .ReadFromJsonAsync<OnboardingStateApiResponse>();
        Assert.NotNull(completed);
        Assert.True(completed.IsCompleted);
        Assert.Null(completed.StepId);
        Assert.Equal("guided", completed.Outcome);

        using var updateCompleted = await firstUser.PutAsJsonAsync(
            "/api/v1/onboarding/progress",
            new SaveOnboardingProgressRequest("navigation-welcome", null));
        Assert.Equal(HttpStatusCode.Conflict, updateCompleted.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var firstUserId = await db.Users
                .Where(x => x.Email == "guide-a@example.test")
                .Select(x => x.Id)
                .SingleAsync();
            var secondUserId = await db.Users
                .Where(x => x.Email == "guide-b@example.test")
                .Select(x => x.Id)
                .SingleAsync();
            Assert.True(await db.UserTokens.AnyAsync(x =>
                x.UserId == firstUserId));
            Assert.False(await db.UserTokens.AnyAsync(x =>
                x.UserId == secondUserId));
        }

        using var reset = await firstUser.DeleteAsync("/api/v1/onboarding");
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var resetState = await reset.Content
            .ReadFromJsonAsync<OnboardingStateApiResponse>();
        Assert.NotNull(resetState);
        Assert.False(resetState.IsCompleted);
        Assert.Null(resetState.StepId);
        Assert.Null(resetState.Outcome);
    }

    [Fact]
    public async Task ProgressApi_ReturnsOnlyOwnedSnapshots_AndClearsSelectedScope()
    {
        using var factory = new GymPlannerApiFactory();
        using var firstUser = CreateClient(factory);
        using var secondUser = CreateClient(factory);
        using var firstRegistration = await firstUser.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("progress-a@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, firstRegistration.StatusCode);
        using var secondRegistration = await secondUser.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("progress-b@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, secondRegistration.StatusCode);
        SetBearer(
            firstUser,
            (await LoginAsync(
                firstUser,
                "progress-a@example.test",
                "password1",
                "Progress A")).AccessToken);
        SetBearer(
            secondUser,
            (await LoginAsync(
                secondUser,
                "progress-b@example.test",
                "password1",
                "Progress B")).AccessToken);

        var firstPlans = await firstUser.GetFromJsonAsync<
            List<TrainingPlanApiResponse>>("/api/v1/training-plans");
        Assert.NotNull(firstPlans);
        var firstPlan = firstPlans[0];

        string secondUserId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var firstUserId = await db.Users
                .Where(x => x.Email == "progress-a@example.test")
                .Select(x => x.Id)
                .SingleAsync();
            secondUserId = await db.Users
                .Where(x => x.Email == "progress-b@example.test")
                .Select(x => x.Id)
                .SingleAsync();
            db.ProgressSnapshots.AddRange(
                new WorkoutPlanner.Web.Models.ProgressSnapshot
                {
                    UserId = firstUserId,
                    WorkoutName = firstPlan.WorkoutName,
                    Date = new DateTime(2026, 1, 1),
                    Score = 100
                },
                new WorkoutPlanner.Web.Models.ProgressSnapshot
                {
                    UserId = firstUserId,
                    WorkoutName = firstPlan.WorkoutName,
                    Date = new DateTime(2026, 1, 2),
                    Score = 150
                },
                new WorkoutPlanner.Web.Models.ProgressSnapshot
                {
                    UserId = secondUserId,
                    WorkoutName = firstPlan.WorkoutName,
                    Date = new DateTime(2026, 1, 1),
                    Score = 999
                });
            db.ExerciseProgressSnapshots.AddRange(
                new WorkoutPlanner.Web.Models.ExerciseProgressSnapshot
                {
                    UserId = firstUserId,
                    WorkoutName = firstPlan.WorkoutName,
                    ExerciseName = "Bench press",
                    Date = new DateTime(2026, 1, 1),
                    Score = 10
                },
                new WorkoutPlanner.Web.Models.ExerciseProgressSnapshot
                {
                    UserId = firstUserId,
                    WorkoutName = firstPlan.WorkoutName,
                    ExerciseName = "Bench press",
                    Date = new DateTime(2026, 1, 2),
                    Score = 20
                },
                new WorkoutPlanner.Web.Models.ExerciseProgressSnapshot
                {
                    UserId = secondUserId,
                    WorkoutName = firstPlan.WorkoutName,
                    ExerciseName = "Private exercise",
                    Date = new DateTime(2026, 1, 1),
                    Score = 999
                });
            await db.SaveChangesAsync();
        }

        var workoutProgress = await firstUser.GetFromJsonAsync<
            WorkoutProgressApiResponse>(
                $"/api/v1/progress/workouts/{firstPlan.Id}");
        Assert.NotNull(workoutProgress);
        Assert.Equal([100d, 150d], workoutProgress.Snapshots.Select(x => x.Score));
        Assert.Equal([0m, 50m], workoutProgress.Chart.Select(x => x.Percent));

        var exerciseNames = await firstUser.GetFromJsonAsync<List<string>>(
            $"/api/v1/progress/workouts/{firstPlan.Id}/exercises");
        Assert.NotNull(exerciseNames);
        Assert.Equal(["Bench press"], exerciseNames);
        var exerciseProgress = await firstUser.GetFromJsonAsync<
            ExerciseProgressApiResponse>(
                $"/api/v1/progress/workouts/{firstPlan.Id}/exercises/chart" +
                "?exerciseName=Bench%20press");
        Assert.NotNull(exerciseProgress);
        Assert.Equal([0m, 100m], exerciseProgress.Chart.Select(x => x.Percent));

        using var foreignRead = await secondUser.GetAsync(
            $"/api/v1/progress/workouts/{firstPlan.Id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);
        using var foreignDelete = await secondUser.DeleteAsync(
            $"/api/v1/progress/workouts/{firstPlan.Id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);

        using var clearExercise = await firstUser.DeleteAsync(
            $"/api/v1/progress/workouts/{firstPlan.Id}/exercises" +
            "?exerciseName=Bench%20press");
        Assert.Equal(HttpStatusCode.NoContent, clearExercise.StatusCode);
        using var clearWorkout = await firstUser.DeleteAsync(
            $"/api/v1/progress/workouts/{firstPlan.Id}");
        Assert.Equal(HttpStatusCode.NoContent, clearWorkout.StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationFactory = verificationScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
        await using var verificationDb =
            await verificationFactory.CreateDbContextAsync();
        Assert.False(await verificationDb.ProgressSnapshots.AnyAsync(x =>
            x.UserId != secondUserId));
        Assert.False(await verificationDb.ExerciseProgressSnapshots.AnyAsync(x =>
            x.UserId != secondUserId));
        Assert.True(await verificationDb.ProgressSnapshots.AnyAsync(x =>
            x.UserId == secondUserId));
        Assert.True(await verificationDb.ExerciseProgressSnapshots.AnyAsync(x =>
            x.UserId == secondUserId));
    }

    [Fact]
    public async Task WorkoutLifecycleApi_CompletesAtomically_AndReturnsImmutableSnapshot()
    {
        using var factory = new GymPlannerApiFactory();
        using var firstUser = CreateClient(factory);
        using var secondUser = CreateClient(factory);

        using var anonymousCalendar = await firstUser.GetAsync("/api/v1/calendar");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCalendar.StatusCode);

        using var firstRegistration = await firstUser.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("lifecycle-a@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, firstRegistration.StatusCode);
        using var secondRegistration = await secondUser.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("lifecycle-b@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, secondRegistration.StatusCode);
        SetBearer(
            firstUser,
            (await LoginAsync(
                firstUser,
                "lifecycle-a@example.test",
                "password1",
                "Lifecycle A")).AccessToken);
        SetBearer(
            secondUser,
            (await LoginAsync(
                secondUser,
                "lifecycle-b@example.test",
                "password1",
                "Lifecycle B")).AccessToken);

        using var createPlan = await firstUser.PostAsJsonAsync(
            "/api/v1/training-plans",
            new CreateTrainingPlanRequest("Lifecycle plan"));
        var plan = await createPlan.Content
            .ReadFromJsonAsync<TrainingPlanApiResponse>();
        Assert.NotNull(plan);

        var exerciseRequest = new SaveExerciseRequest(
            "Lifecycle exercise",
            2,
            "NotCompleted",
            null,
            [
                new SaveExerciseSetRequest(1, 8, 42.5, true),
                new SaveExerciseSetRequest(2, 6, 47.5, false)
            ]);
        using var createExercise = await firstUser.PostAsJsonAsync(
            $"/api/v1/training-plans/{plan.Id}/exercises",
            exerciseRequest);
        var exercise = await createExercise.Content
            .ReadFromJsonAsync<ExerciseApiResponse>();
        Assert.NotNull(exercise);

        using var schedule = await firstUser.PostAsJsonAsync(
            "/api/v1/calendar",
            new ScheduleWorkoutRequest(DateTime.Today, plan.Id));
        Assert.Equal(HttpStatusCode.Created, schedule.StatusCode);
        var day = await schedule.Content.ReadFromJsonAsync<WorkoutDayApiResponse>();
        Assert.NotNull(day);

        using var foreignSchedule = await secondUser.PostAsJsonAsync(
            "/api/v1/calendar",
            new ScheduleWorkoutRequest(DateTime.Today, plan.Id));
        Assert.Equal(HttpStatusCode.NotFound, foreignSchedule.StatusCode);
        using var foreignDay = await secondUser.GetAsync(
            $"/api/v1/calendar/{day.Id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignDay.StatusCode);
        using var foreignDayDelete = await secondUser.DeleteAsync(
            $"/api/v1/calendar/{day.Id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignDayDelete.StatusCode);
        using var foreignStart = await secondUser.PostAsync(
            "/api/v1/workouts/today/start",
            content: null);
        Assert.Equal(HttpStatusCode.NotFound, foreignStart.StatusCode);

        using var start = await firstUser.PostAsync(
            "/api/v1/workouts/today/start",
            content: null);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var started = await start.Content
            .ReadFromJsonAsync<TodayWorkoutApiResponse>();
        Assert.NotNull(started);
        Assert.Equal(plan.Id, started.TrainingPlan.Id);
        Assert.Equal([42.5d, 47.5d], started.TrainingPlan.Exercises
            .Single(x => x.Id == exercise.Id)
            .Sets.Select(x => x.Weight));

        using var incomplete = await firstUser.PostAsync(
            "/api/v1/workouts/today/complete",
            content: null);
        Assert.Equal(HttpStatusCode.Conflict, incomplete.StatusCode);

        using var updateExercise = await firstUser.PutAsJsonAsync(
            $"/api/v1/exercises/{exercise.Id}",
            exerciseRequest with { Status = "Hard" });
        Assert.Equal(HttpStatusCode.OK, updateExercise.StatusCode);

        using var complete = await firstUser.PostAsync(
            "/api/v1/workouts/today/complete",
            content: null);
        Assert.Equal(HttpStatusCode.Created, complete.StatusCode);
        var completedHistory = await complete.Content
            .ReadFromJsonAsync<WorkoutHistoryApiResponse>();
        Assert.NotNull(completedHistory);
        Assert.True(completedHistory.SnapshotAvailable);
        Assert.Equal("Hard", Assert.Single(completedHistory.Exercises).Status);
        Assert.Equal(
            [42.5d, 47.5d],
            completedHistory.Exercises[0].Sets.Select(x => x.Weight));

        using var duplicateCompletion = await firstUser.PostAsync(
            "/api/v1/workouts/today/complete",
            content: null);
        Assert.Equal(HttpStatusCode.Conflict, duplicateCompletion.StatusCode);
        using var noActiveToday = await firstUser.GetAsync(
            "/api/v1/workouts/today");
        Assert.Equal(HttpStatusCode.NotFound, noActiveToday.StatusCode);

        var completedDay = await firstUser.GetFromJsonAsync<WorkoutDayApiResponse>(
            $"/api/v1/calendar/{day.Id}");
        Assert.NotNull(completedDay);
        Assert.True(completedDay.IsCompleted);
        var secondHistory = await secondUser.GetFromJsonAsync<
            List<WorkoutHistoryApiResponse>>("/api/v1/history");
        Assert.NotNull(secondHistory);
        Assert.Empty(secondHistory);
        using var foreignHistory = await secondUser.GetAsync(
            $"/api/v1/history/{completedHistory.Id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignHistory.StatusCode);
        using var foreignHistoryDelete = await secondUser.DeleteAsync(
            $"/api/v1/history/{completedHistory.Id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignHistoryDelete.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var firstUserId = await db.Users
                .Where(x => x.Email == "lifecycle-a@example.test")
                .Select(x => x.Id)
                .SingleAsync();
            var storedHistory = await db.WorkoutHistory
                .AsNoTracking()
                .SingleAsync(x => x.Id == completedHistory.Id);
            Assert.Equal(firstUserId, storedHistory.UserId);
            Assert.Contains("42.5", storedHistory.Details, StringComparison.Ordinal);
            Assert.Contains("47.5", storedHistory.Details, StringComparison.Ordinal);
            Assert.Single(await db.ProgressSnapshots
                .Where(x => x.UserId == firstUserId)
                .ToListAsync());
            Assert.Single(await db.ExerciseProgressSnapshots
                .Where(x => x.UserId == firstUserId)
                .ToListAsync());
            Assert.False(await db.TrainingSessions.AnyAsync(x =>
                x.UserId == firstUserId));

            db.WorkoutHistory.Add(new WorkoutPlanner.Web.Models.WorkoutHistory
            {
                UserId = firstUserId,
                WorkoutName = "Legacy malformed snapshot",
                Date = DateTime.UtcNow.AddDays(-1),
                Details = "not-json"
            });
            await db.SaveChangesAsync();
        }

        var history = await firstUser.GetFromJsonAsync<
            List<WorkoutHistoryApiResponse>>("/api/v1/history");
        Assert.NotNull(history);
        Assert.Contains(history, x =>
            x.Id == completedHistory.Id && x.SnapshotAvailable);
        Assert.Contains(history, x =>
            x.WorkoutName == "Legacy malformed snapshot" &&
            !x.SnapshotAvailable &&
            x.Exercises.Count == 0);

        using var deleteHistory = await firstUser.DeleteAsync(
            $"/api/v1/history/{completedHistory.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteHistory.StatusCode);
        using var deletedHistory = await firstUser.GetAsync(
            $"/api/v1/history/{completedHistory.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deletedHistory.StatusCode);
    }

    [Fact]
    public async Task WorkoutApi_PreservesPerSetWeights_AndRejectsCrossUserAccess()
    {
        using var factory = new GymPlannerApiFactory();
        using var firstUser = CreateClient(factory);
        using var secondUser = CreateClient(factory);

        using var anonymousPlans = await firstUser.GetAsync(
            "/api/v1/training-plans");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPlans.StatusCode);

        using var firstRegistration = await firstUser.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("workout-a@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, firstRegistration.StatusCode);
        using var secondRegistration = await secondUser.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("workout-b@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, secondRegistration.StatusCode);

        SetBearer(
            firstUser,
            (await LoginAsync(
                firstUser,
                "workout-a@example.test",
                "password1",
                "First user phone")).AccessToken);
        SetBearer(
            secondUser,
            (await LoginAsync(
                secondUser,
                "workout-b@example.test",
                "password1",
                "Second user phone")).AccessToken);

        using var createPlan = await firstUser.PostAsJsonAsync(
            "/api/v1/training-plans",
            new CreateTrainingPlanRequest("API strength"));
        Assert.Equal(HttpStatusCode.Created, createPlan.StatusCode);
        var plan = await createPlan.Content
            .ReadFromJsonAsync<TrainingPlanApiResponse>();
        Assert.NotNull(plan);
        Assert.Equal(
            $"/api/v1/training-plans/{plan.Id}",
            createPlan.Headers.Location?.OriginalString);

        using var duplicatePlan = await firstUser.PostAsJsonAsync(
            "/api/v1/training-plans",
            new CreateTrainingPlanRequest("API strength"));
        Assert.Equal(HttpStatusCode.Conflict, duplicatePlan.StatusCode);

        using var invalidExercise = await firstUser.PostAsJsonAsync(
            $"/api/v1/training-plans/{plan.Id}/exercises",
            new SaveExerciseRequest(
                "Invalid exercise",
                3,
                "NotCompleted",
                null,
                [new SaveExerciseSetRequest(1, 8, 40, false)]));
        Assert.Equal(HttpStatusCode.BadRequest, invalidExercise.StatusCode);
        Assert.Equal(
            "application/problem+json",
            invalidExercise.Content.Headers.ContentType?.MediaType);

        var createExerciseRequest = new SaveExerciseRequest(
            "Bench press",
            3,
            "NotCompleted",
            null,
            [
                new SaveExerciseSetRequest(1, 8, 60, false),
                new SaveExerciseSetRequest(2, 7, 65, false),
                new SaveExerciseSetRequest(3, 6, 70, false)
            ]);
        using var createExercise = await firstUser.PostAsJsonAsync(
            $"/api/v1/training-plans/{plan.Id}/exercises",
            createExerciseRequest);
        Assert.Equal(HttpStatusCode.Created, createExercise.StatusCode);
        var exercise = await createExercise.Content
            .ReadFromJsonAsync<ExerciseApiResponse>();
        Assert.NotNull(exercise);
        Assert.Equal([60d, 65d, 70d], exercise.Sets.Select(x => x.Weight));

        using var foreignPlan = await secondUser.GetAsync(
            $"/api/v1/training-plans/{plan.Id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignPlan.StatusCode);
        using var foreignPlanRename = await secondUser.PutAsJsonAsync(
            $"/api/v1/training-plans/{plan.Id}",
            new RenameTrainingPlanRequest("Compromised plan"));
        Assert.Equal(HttpStatusCode.NotFound, foreignPlanRename.StatusCode);
        using var foreignPlanDelete = await secondUser.DeleteAsync(
            $"/api/v1/training-plans/{plan.Id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignPlanDelete.StatusCode);
        using var foreignPlanExercises = await secondUser.GetAsync(
            $"/api/v1/training-plans/{plan.Id}/exercises");
        Assert.Equal(HttpStatusCode.NotFound, foreignPlanExercises.StatusCode);
        using var foreignExercise = await secondUser.GetAsync(
            $"/api/v1/exercises/{exercise.Id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignExercise.StatusCode);

        var foreignUpdateRequest = createExerciseRequest with
        {
            Name = "Compromised exercise",
            Sets =
            [
                new SaveExerciseSetRequest(1, 1, 1, true),
                new SaveExerciseSetRequest(2, 1, 1, true),
                new SaveExerciseSetRequest(3, 1, 1, true)
            ]
        };
        using var foreignUpdate = await secondUser.PutAsJsonAsync(
            $"/api/v1/exercises/{exercise.Id}",
            foreignUpdateRequest);
        Assert.Equal(HttpStatusCode.NotFound, foreignUpdate.StatusCode);
        using var foreignDelete = await secondUser.DeleteAsync(
            $"/api/v1/exercises/{exercise.Id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);

        var ownUpdateRequest = createExerciseRequest with
        {
            Sets =
            [
                new SaveExerciseSetRequest(1, 10, 62.5, true),
                new SaveExerciseSetRequest(2, 9, 67.5, false),
                new SaveExerciseSetRequest(3, 8, 72.5, false)
            ]
        };
        using var ownUpdate = await firstUser.PutAsJsonAsync(
            $"/api/v1/exercises/{exercise.Id}",
            ownUpdateRequest);
        Assert.Equal(HttpStatusCode.OK, ownUpdate.StatusCode);
        var updated = await ownUpdate.Content
            .ReadFromJsonAsync<ExerciseApiResponse>();
        Assert.NotNull(updated);
        Assert.Equal(
            [62.5d, 67.5d, 72.5d],
            updated.Sets.Select(x => x.Weight));
        Assert.Equal([10, 9, 8], updated.Sets.Select(x => x.Repetitions));

        var secondUserPlans = await secondUser.GetFromJsonAsync<
            List<TrainingPlanApiResponse>>("/api/v1/training-plans");
        Assert.NotNull(secondUserPlans);
        Assert.DoesNotContain(secondUserPlans, x => x.Id == plan.Id);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        var firstUserId = await db.Users
            .Where(x => x.Email == "workout-a@example.test")
            .Select(x => x.Id)
            .SingleAsync();
        var savedExercise = await db.Exercises
            .AsNoTracking()
            .Include(x => x.Sets)
            .SingleAsync(x => x.Id == exercise.Id);
        Assert.Equal(firstUserId, savedExercise.UserId);
        Assert.Equal("Bench press", savedExercise.Name);
        Assert.Equal(
            [62.5d, 67.5d, 72.5d],
            savedExercise.Sets
                .OrderBy(x => x.SetNumber)
                .Select(x => x.Weight));

        using var renamePlan = await firstUser.PutAsJsonAsync(
            $"/api/v1/training-plans/{plan.Id}",
            new RenameTrainingPlanRequest("API strength updated"));
        Assert.Equal(HttpStatusCode.OK, renamePlan.StatusCode);
        var renamedPlan = await renamePlan.Content
            .ReadFromJsonAsync<TrainingPlanApiResponse>();
        Assert.NotNull(renamedPlan);
        Assert.Equal("API strength updated", renamedPlan.WorkoutName);
        Assert.All(
            renamedPlan.Exercises,
            item => Assert.Equal(plan.Id, item.TrainingPlanId));

        using var deletePlan = await firstUser.DeleteAsync(
            $"/api/v1/training-plans/{plan.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deletePlan.StatusCode);
        using var deletedPlan = await firstUser.GetAsync(
            $"/api/v1/training-plans/{plan.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deletedPlan.StatusCode);
        Assert.False(await db.TrainingPlans.AnyAsync(x => x.Id == plan.Id));
        Assert.False(await db.Exercises.AnyAsync(x => x.Id == exercise.Id));
    }

    [Fact]
    public async Task SessionAndAccountLifecycle_RevokesTokensAndDeletesOwnedData()
    {
        using var factory = new GymPlannerApiFactory();
        using var firstDevice = CreateClient(factory);
        using var secondDevice = CreateClient(factory);

        using var registration = await firstDevice.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("lifecycle@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        var firstTokens = await LoginAsync(
            firstDevice,
            "lifecycle@example.test",
            "password1",
            "First phone");
        var secondTokens = await LoginAsync(
            secondDevice,
            "lifecycle@example.test",
            "password1",
            "Second phone");
        SetBearer(firstDevice, firstTokens.AccessToken);
        SetBearer(secondDevice, secondTokens.AccessToken);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var deviceNames = await db.MobileSessions
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => x.DeviceName)
                .ToArrayAsync();
            Assert.Collection(
                deviceNames,
                value => Assert.Equal("First phone", value),
                value => Assert.Equal("Second phone", value));
        }

        using var logout = await firstDevice.PostAsync(
            "/api/v1/auth/logout",
            content: null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var firstAfterLogout = await firstDevice.GetAsync("/api/v1/profile");
        Assert.Equal(HttpStatusCode.Forbidden, firstAfterLogout.StatusCode);
        using var secondAfterLogout = await secondDevice.GetAsync("/api/v1/profile");
        Assert.Equal(HttpStatusCode.OK, secondAfterLogout.StatusCode);
        using var firstRefresh = await firstDevice.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new MobileRefreshRequest(firstTokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, firstRefresh.StatusCode);

        using var revokeAll = await secondDevice.PostAsync(
            "/api/v1/account/revoke-access",
            content: null);
        Assert.Equal(HttpStatusCode.NoContent, revokeAll.StatusCode);
        using var secondAfterRevoke = await secondDevice.GetAsync("/api/v1/profile");
        Assert.Equal(HttpStatusCode.Forbidden, secondAfterRevoke.StatusCode);
        using var secondRefresh = await secondDevice.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new MobileRefreshRequest(secondTokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, secondRefresh.StatusCode);

        using var passwordClient = CreateClient(factory);
        var passwordTokens = await LoginAsync(
            passwordClient,
            "lifecycle@example.test",
            "password1",
            "Password phone");
        SetBearer(passwordClient, passwordTokens.AccessToken);
        using var passwordChange = await passwordClient.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new ChangePasswordApiRequest("password1", "newpassword1"));
        Assert.Equal(HttpStatusCode.NoContent, passwordChange.StatusCode);
        using var afterPasswordChange = await passwordClient.GetAsync(
            "/api/v1/profile");
        Assert.Equal(HttpStatusCode.Forbidden, afterPasswordChange.StatusCode);
        using var passwordRefresh = await passwordClient.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new MobileRefreshRequest(passwordTokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, passwordRefresh.StatusCode);

        using var oldPasswordLogin = await passwordClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new MobileLoginRequest(
                "lifecycle@example.test",
                "password1",
                "Old password"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        using var deletionClient = CreateClient(factory);
        var deletionTokens = await LoginAsync(
            deletionClient,
            "lifecycle@example.test",
            "newpassword1",
            "Deletion phone");
        SetBearer(deletionClient, deletionTokens.AccessToken);
        using var deletion = await deletionClient.DeleteAsync("/api/v1/account");
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        using var afterDeletion = await deletionClient.GetAsync("/api/v1/profile");
        Assert.Equal(HttpStatusCode.Forbidden, afterDeletion.StatusCode);
        using var deletedLogin = await deletionClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new MobileLoginRequest(
                "lifecycle@example.test",
                "newpassword1",
                "Deleted account"));
        Assert.Equal(HttpStatusCode.Unauthorized, deletedLogin.StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationFactory = verificationScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
        await using var verificationDb =
            await verificationFactory.CreateDbContextAsync();
        Assert.False(await verificationDb.Users.AnyAsync(x =>
            x.Email == "lifecycle@example.test"));
        Assert.False(await verificationDb.MobileSessions.AnyAsync());
        Assert.False(await verificationDb.TrainingPlans.AnyAsync(x =>
            x.UserId != null));
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    private static async Task<AccessTokenResponse> LoginAsync(
        HttpClient client,
        string email,
        string password,
        string deviceName)
    {
        using var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new MobileLoginRequest(email, password, deviceName));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.NotNull(tokens);
        return tokens;
    }

    private static void SetBearer(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static MultipartFormDataContent CreatePhotoContent(
        byte[] bytes,
        string contentType,
        string fileName)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    private sealed class GymPlannerApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _environmentName;
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            "GymPlanner.Tests",
            $"api-{Guid.NewGuid():N}.db");

        public GymPlannerApiFactory(string environmentName = "Testing")
        {
            _environmentName = environmentName;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            builder.UseEnvironment(_environmentName);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:WorkoutDatabase"] =
                        $"Data Source={_databasePath}"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection()
                    .UseEphemeralDataProtectionProvider();
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            SqliteConnection.ClearAllPools();
            TryDelete(_databasePath);
            TryDelete($"{_databasePath}-wal");
            TryDelete($"{_databasePath}-shm");
        }

        private static void TryDelete(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
