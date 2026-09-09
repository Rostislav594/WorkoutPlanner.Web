using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkoutPlanner.Api.Contracts;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services.Auth;
using WorkoutPlanner.Web.Services.Support;

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
        Assert.Contains("MobileTokenResponse", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/profile", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/training-plans", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/exercises/{id}", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/calendar", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/history", document, StringComparison.Ordinal);
        Assert.Contains(
            "/api/v1/progress/workouts/{trainingPlanId}",
            document,
            StringComparison.Ordinal);
        Assert.Contains("/api/v1/welcome-guide", document, StringComparison.Ordinal);
        Assert.Contains(
            "/api/v1/exercises/{exerciseId}/photo",
            document,
            StringComparison.Ordinal);
        Assert.Contains(
            "/api/v1/support/tickets",
            document,
            StringComparison.Ordinal);
        Assert.Contains("/api/watch/pairing-codes", document, StringComparison.Ordinal);
        Assert.Contains("/api/watch/pair", document, StringComparison.Ordinal);
        Assert.Contains("/api/watch/token/refresh", document, StringComparison.Ordinal);
        Assert.Contains("/api/watch/devices", document, StringComparison.Ordinal);
        Assert.Contains("/api/watch/workouts/active", document, StringComparison.Ordinal);
        Assert.Contains(
            "/api/watch/sets/{setId}/complete",
            document,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DevelopmentApi_AllowsHttpWithoutHttpsRedirect()
    {
        using var factory = new GymPlannerApiFactory("Development");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/api/v1/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
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
                0,
                await db.TrainingPlans.CountAsync(x => x.UserId == userId));
            Assert.False(await db.TrainingPlans.AnyAsync(x => x.UserId == null));
        }

        using var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new MobileLoginRequest("mobile-a@example.test", "password1"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<MobileTokenResponse>();
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
        Assert.Equal(90, savedProfile.RestBetweenSetsSeconds);
        Assert.Equal(120, savedProfile.RestBetweenExercisesSeconds);

        using var restTimerUpdate = await client.PutAsJsonAsync(
            "/api/v1/profile/rest-timers",
            new UpdateRestTimerSettingsRequest(75, 150));
        Assert.Equal(HttpStatusCode.OK, restTimerUpdate.StatusCode);
        var restTimerSettings = await restTimerUpdate.Content
            .ReadFromJsonAsync<RestTimerSettingsResponse>();
        Assert.NotNull(restTimerSettings);
        Assert.Equal(75, restTimerSettings.RestBetweenSetsSeconds);
        Assert.Equal(150, restTimerSettings.RestBetweenExercisesSeconds);

        var profileWithTimers = await client.GetFromJsonAsync<ProfileResponse>(
            "/api/v1/profile");
        Assert.NotNull(profileWithTimers);
        Assert.Equal(75, profileWithTimers.RestBetweenSetsSeconds);
        Assert.Equal(150, profileWithTimers.RestBetweenExercisesSeconds);

        using var refresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new MobileRefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshedTokens = await refresh.Content
            .ReadFromJsonAsync<MobileTokenResponse>();
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
            .ReadFromJsonAsync<MobileTokenResponse>();
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
    public async Task SupportApi_ValidatesAndPersistsTicket_WhenTelegramFails()
    {
        var notification = new FailingSupportNotificationService();
        using var factory = new GymPlannerApiFactory(
            supportNotification: notification);
        using var anonymous = CreateClient(factory);
        using (var anonymousContent = CreateSupportContent(
                   "The application closes after saving a workout."))
        using (var anonymousResponse = await anonymous.PostAsync(
                   "/api/v1/support/tickets",
                   anonymousContent))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        }

        using var client = CreateClient(factory);
        using var registration = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("support@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        SetBearer(
            client,
            (await LoginAsync(
                client,
                "support@example.test",
                "password1",
                "Support test")).AccessToken);

        using (var emptyContent = CreateSupportContent("   "))
        using (var emptyResponse = await client.PostAsync(
                   "/api/v1/support/tickets",
                   emptyContent))
        {
            Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);
        }

        using (var longContent = CreateSupportContent(new string('x', 2001)))
        using (var longResponse = await client.PostAsync(
                   "/api/v1/support/tickets",
                   longContent))
        {
            Assert.Equal(HttpStatusCode.BadRequest, longResponse.StatusCode);
        }

        using (var invalidImageContent = CreateSupportContent(
                   "This request contains an invalid screenshot.",
                   [0x01, 0x02, 0x03],
                   "image/png"))
        using (var invalidImageResponse = await client.PostAsync(
                   "/api/v1/support/tickets",
                   invalidImageContent))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalidImageResponse.StatusCode);
        }

        const string message =
            "  The application closes after saving a completed workout.  ";
        using var validContent = CreateSupportContent(
            message,
            appVersion: "1.0.0",
            platform: "Android",
            osVersion: "16",
            deviceModel: "Test Phone");
        using var response = await client.PostAsync(
            "/api/v1/support/tickets",
            validContent);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content
            .ReadFromJsonAsync<SupportTicketResponse>();
        Assert.NotNull(created);
        Assert.Matches(@"^GP-\d{8}-\d{6}$", created.TicketNumber);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        var ticket = await db.SupportTickets.SingleAsync();
        var userId = await db.Users
            .Where(x => x.Email == "support@example.test")
            .Select(x => x.Id)
            .SingleAsync();
        Assert.Equal(userId, ticket.UserId);
        Assert.Equal(message.Trim(), ticket.Message);
        Assert.Equal(SupportTicketStatus.New, ticket.Status);
        Assert.Equal(SupportDeliveryStatus.Failed, ticket.TelegramDeliveryStatus);
        Assert.Equal("Android", ticket.Platform);
        var initialMessage = await db.SupportMessages.SingleAsync();
        Assert.Equal(ticket.Id, initialMessage.SupportTicketId);
        Assert.Equal(SupportMessageSenderType.User, initialMessage.SenderType);
        Assert.Equal(message.Trim(), initialMessage.Message);
        Assert.Equal(1, notification.CallCount);
        Assert.Equal(ticket.TicketNumber, notification.LastTicketNumber);
    }

    [Fact]
    public async Task SupportApi_ValidatesScreenshotSize_AndStoresSafeImage()
    {
        var notification = new FailingSupportNotificationService();
        using var factory = new GymPlannerApiFactory(
            supportNotification: notification);
        using var client = CreateClient(factory);
        using var registration = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("support-image@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        SetBearer(
            client,
            (await LoginAsync(
                client,
                "support-image@example.test",
                "password1",
                "Support image test")).AccessToken);

        using (var oversizedContent = CreateSupportContent(
                   "This screenshot is too large and must be rejected.",
                   new byte[SupportScreenshotStorage.MaxScreenshotSize + 1],
                   "image/png"))
        using (var oversizedResponse = await client.PostAsync(
                   "/api/v1/support/tickets",
                   oversizedContent))
        {
            Assert.Equal(
                HttpStatusCode.RequestEntityTooLarge,
                oversizedResponse.StatusCode);
        }

        byte[] validPng =
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        using var validContent = CreateSupportContent(
            "The screen becomes blank after opening workout history.",
            validPng,
            "image/png");
        using var validResponse = await client.PostAsync(
            "/api/v1/support/tickets",
            validContent);
        Assert.Equal(HttpStatusCode.Created, validResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        var screenshotPath = await db.SupportTickets
            .Select(x => x.ScreenshotPath)
            .SingleAsync();
        Assert.NotNull(screenshotPath);
        Assert.StartsWith(
            "/SupportScreenshots/",
            screenshotPath,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "client-name",
            screenshotPath,
            StringComparison.Ordinal);

        var environment = scope.ServiceProvider
            .GetRequiredService<IWebHostEnvironment>();
        var physicalPath = Path.Combine(
            environment.ContentRootPath,
            "App_Data",
            "SupportScreenshots",
            Path.GetFileName(screenshotPath));
        Assert.True(File.Exists(physicalPath));
        File.Delete(physicalPath);
    }

    [Fact]
    public async Task SupportApi_SucceedsWithoutTelegramConfiguration()
    {
        using var factory = new GymPlannerApiFactory();
        using var client = CreateClient(factory);
        using var registration = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("support-disabled@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        SetBearer(
            client,
            (await LoginAsync(
                client,
                "support-disabled@example.test",
                "password1",
                "Support disabled test")).AccessToken);

        using var content = CreateSupportContent(
            "The support request must remain saved without Telegram.");
        using var response = await client.PostAsync(
            "/api/v1/support/tickets",
            content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        var ticket = await db.SupportTickets.SingleAsync();
        Assert.Equal(
            SupportDeliveryStatus.Disabled,
            ticket.TelegramDeliveryStatus);
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
    public async Task WelcomeGuideApi_PersistsCompletion_PerAuthenticatedUser()
    {
        using var factory = new GymPlannerApiFactory();
        using var firstUser = CreateClient(factory);
        using var secondUser = CreateClient(factory);
        using var anonymousState = await firstUser.GetAsync("/api/v1/welcome-guide");
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

        var initial = await firstUser.GetFromJsonAsync<WelcomeGuideStateApiResponse>(
            "/api/v1/welcome-guide");
        Assert.NotNull(initial);
        Assert.False(initial.IsCompleted);

        var secondState = await secondUser.GetFromJsonAsync<
            WelcomeGuideStateApiResponse>("/api/v1/welcome-guide");
        Assert.NotNull(secondState);
        Assert.False(secondState.IsCompleted);

        using var complete = await firstUser.PostAsync(
            "/api/v1/welcome-guide",
            content: null);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completed = await complete.Content
            .ReadFromJsonAsync<WelcomeGuideStateApiResponse>();
        Assert.NotNull(completed);
        Assert.True(completed.IsCompleted);

        var persisted = await firstUser.GetFromJsonAsync<WelcomeGuideStateApiResponse>(
            "/api/v1/welcome-guide");
        Assert.NotNull(persisted);
        Assert.True(persisted.IsCompleted);

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

        using var createFirstPlan = await firstUser.PostAsJsonAsync(
            "/api/v1/training-plans",
            new CreateTrainingPlanRequest("Progress plan"));
        Assert.Equal(HttpStatusCode.Created, createFirstPlan.StatusCode);
        var firstPlan = await createFirstPlan.Content.ReadFromJsonAsync<TrainingPlanApiResponse>();
        Assert.NotNull(firstPlan);

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
                    Date = DateTime.Today.AddDays(-40),
                    Score = 100
                },
                new WorkoutPlanner.Web.Models.ProgressSnapshot
                {
                    UserId = firstUserId,
                    WorkoutName = firstPlan.WorkoutName,
                    Date = DateTime.Today.AddDays(-20),
                    Score = 150
                },
                new WorkoutPlanner.Web.Models.ProgressSnapshot
                {
                    UserId = firstUserId,
                    WorkoutName = firstPlan.WorkoutName,
                    Date = DateTime.Today.AddDays(-1),
                    Score = 200
                },
                new WorkoutPlanner.Web.Models.ProgressSnapshot
                {
                    UserId = secondUserId,
                    WorkoutName = firstPlan.WorkoutName,
                    Date = DateTime.Today.AddDays(-1),
                    Score = 999
                });
            db.ExerciseProgressSnapshots.AddRange(
                new WorkoutPlanner.Web.Models.ExerciseProgressSnapshot
                {
                    UserId = firstUserId,
                    WorkoutName = firstPlan.WorkoutName,
                    ExerciseName = "Bench press",
                    Date = DateTime.Today.AddDays(-40),
                    Score = 10
                },
                new WorkoutPlanner.Web.Models.ExerciseProgressSnapshot
                {
                    UserId = firstUserId,
                    WorkoutName = firstPlan.WorkoutName,
                    ExerciseName = "Bench press",
                    Date = DateTime.Today.AddDays(-20),
                    Score = 20
                },
                new WorkoutPlanner.Web.Models.ExerciseProgressSnapshot
                {
                    UserId = firstUserId,
                    WorkoutName = firstPlan.WorkoutName,
                    ExerciseName = "Bench press",
                    Date = DateTime.Today.AddDays(-1),
                    Score = 30
                },
                new WorkoutPlanner.Web.Models.ExerciseProgressSnapshot
                {
                    UserId = secondUserId,
                    WorkoutName = firstPlan.WorkoutName,
                    ExerciseName = "Private exercise",
                    Date = DateTime.Today.AddDays(-1),
                    Score = 999
                });
            await db.SaveChangesAsync();
        }

        var workoutProgress = await firstUser.GetFromJsonAsync<
            WorkoutProgressApiResponse>(
                $"/api/v1/progress/workouts/{firstPlan.Id}");
        Assert.NotNull(workoutProgress);
        Assert.Equal([100d, 150d, 200d], workoutProgress.Snapshots.Select(x => x.Score));
        Assert.Equal([50m, 100m], workoutProgress.Chart.Select(x => x.Percent));
        Assert.Equal(
            [DateTime.Today.AddDays(-20), DateTime.Today.AddDays(-1)],
            workoutProgress.Chart.Select(x => x.Date.Date));

        var exerciseNames = await firstUser.GetFromJsonAsync<List<string>>(
            $"/api/v1/progress/workouts/{firstPlan.Id}/exercises");
        Assert.NotNull(exerciseNames);
        Assert.Equal(["Bench press"], exerciseNames);
        var exerciseProgress = await firstUser.GetFromJsonAsync<
            ExerciseProgressApiResponse>(
                $"/api/v1/progress/workouts/{firstPlan.Id}/exercises/chart" +
                "?exerciseName=Bench%20press");
        Assert.NotNull(exerciseProgress);
        Assert.Equal([100m, 200m], exerciseProgress.Chart.Select(x => x.Percent));

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
                new SaveExerciseSetRequest(1, 8, 42.5, true, true),
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

        using var moveToTomorrow = await firstUser.PutAsJsonAsync(
            $"/api/v1/calendar/{day.Id}/date",
            new MoveWorkoutRequest(DateTime.Today.AddDays(1)));
        Assert.Equal(HttpStatusCode.OK, moveToTomorrow.StatusCode);
        var movedDay = await moveToTomorrow.Content.ReadFromJsonAsync<WorkoutDayApiResponse>();
        Assert.NotNull(movedDay);
        Assert.Equal(DateTime.Today.AddDays(1), movedDay.Date.Date);

        using var moveBack = await firstUser.PutAsJsonAsync(
            $"/api/v1/calendar/{day.Id}/date",
            new MoveWorkoutRequest(DateTime.Today));
        Assert.Equal(HttpStatusCode.OK, moveBack.StatusCode);

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
        Assert.True(started.TrainingPlan.Exercises
            .Single(x => x.Id == exercise.Id)
            .Sets[0].IsWarmup);

        using var complete = await firstUser.PostAsync(
            "/api/v1/workouts/today/complete",
            content: null);
        Assert.Equal(HttpStatusCode.Created, complete.StatusCode);
        var completedHistory = await complete.Content
            .ReadFromJsonAsync<WorkoutHistoryApiResponse>();
        Assert.NotNull(completedHistory);
        Assert.True(completedHistory.SnapshotAvailable);
        Assert.Equal("NotCompleted", Assert.Single(completedHistory.Exercises).Status);
        Assert.Equal(
            [42.5d, 47.5d],
            completedHistory.Exercises[0].Sets.Select(x => x.Weight));
        Assert.True(completedHistory.Exercises[0].Sets[0].IsWarmup);

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
    public async Task FreeWorkoutApi_SavesOnlyHistoryOrCreatesTemplateProgress_WithoutCalendar()
    {
        using var factory = new GymPlannerApiFactory();
        using var client = CreateClient(factory);
        using var registration = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("free-workout@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        SetBearer(
            client,
            (await LoginAsync(
                client,
                "free-workout@example.test",
                "password1",
                "Free workout phone")).AccessToken);

        var definitions = await client.GetFromJsonAsync<
            List<ExerciseDefinitionApiResponse>>("/api/v1/exercise-definitions");
        var definition = Assert.Single(definitions!.Take(1));
        var exercise = new SaveExerciseRequest(
            definition.Name,
            2,
            "Hard",
            definition.Id,
            [
                new SaveExerciseSetRequest(1, 10, 25, true, true),
                new SaveExerciseSetRequest(2, 8, 35, true, false)
            ]);

        string userId;
        int plansBefore;
        int daysBefore;
        int workoutProgressBefore;
        int exerciseProgressBefore;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            userId = await db.Users
                .Where(x => x.Email == "free-workout@example.test")
                .Select(x => x.Id)
                .SingleAsync();
            plansBefore = await db.TrainingPlans.CountAsync(x => x.UserId == userId);
            daysBefore = await db.WorkoutDays.CountAsync(x => x.UserId == userId);
            workoutProgressBefore = await db.ProgressSnapshots.CountAsync(x => x.UserId == userId);
            exerciseProgressBefore = await db.ExerciseProgressSnapshots.CountAsync(x => x.UserId == userId);
        }

        using var historyOnlyResponse = await client.PostAsJsonAsync(
            "/api/v1/workouts/free/complete",
            new CompleteFreeWorkoutRequest(false, null, [exercise]));
        Assert.Equal(HttpStatusCode.Created, historyOnlyResponse.StatusCode);
        var historyOnly = await historyOnlyResponse.Content
            .ReadFromJsonAsync<CompleteFreeWorkoutResponse>();
        Assert.NotNull(historyOnly);
        Assert.Null(historyOnly.TrainingPlanId);
        Assert.Equal("Свободная тренировка", historyOnly.History.WorkoutName);
        Assert.True(historyOnly.History.SnapshotAvailable);
        Assert.All(historyOnly.History.Exercises[0].Sets, x => Assert.True(x.Completed));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            Assert.Equal(plansBefore, await db.TrainingPlans.CountAsync(x => x.UserId == userId));
            Assert.Equal(daysBefore, await db.WorkoutDays.CountAsync(x => x.UserId == userId));
            Assert.Equal(workoutProgressBefore, await db.ProgressSnapshots.CountAsync(x => x.UserId == userId));
            Assert.Equal(exerciseProgressBefore, await db.ExerciseProgressSnapshots.CountAsync(x => x.UserId == userId));
            Assert.True(await db.WorkoutHistory.AnyAsync(x =>
                x.Id == historyOnly.History.Id && x.UserId == userId));
        }

        const int supersetGroupId = 17;
        var firstSupersetExercise = exercise with { SupersetGroupId = supersetGroupId };
        var secondSupersetExercise = new SaveExerciseRequest(
            $"{definition.Name} 2",
            2,
            "Medium",
            definition.Id,
            [
                new SaveExerciseSetRequest(1, 12, 20, true),
                new SaveExerciseSetRequest(2, 10, 30, true)
            ],
            supersetGroupId);

        using var templateResponse = await client.PostAsJsonAsync(
            "/api/v1/workouts/free/complete",
            new CompleteFreeWorkoutRequest(
                true,
                "Шаблон из свободной",
                [firstSupersetExercise, secondSupersetExercise]));
        Assert.Equal(HttpStatusCode.Created, templateResponse.StatusCode);
        var templateResult = await templateResponse.Content
            .ReadFromJsonAsync<CompleteFreeWorkoutResponse>();
        Assert.NotNull(templateResult);
        Assert.NotNull(templateResult.TrainingPlanId);
        Assert.Equal("Шаблон из свободной", templateResult.History.WorkoutName);
        Assert.Equal("Hard", templateResult.History.Exercises[0].Status);
        Assert.Equal(2, templateResult.History.Exercises.Count);
        Assert.All(templateResult.History.Exercises, item =>
        {
            Assert.Equal(supersetGroupId, item.SupersetGroupId);
            Assert.All(item.Sets, set => Assert.True(set.Completed));
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var storedTemplate = await db.TrainingPlans
                .AsNoTracking()
                .Include(x => x.Exercises)
                    .ThenInclude(x => x.Sets)
                .SingleAsync(x =>
                    x.Id == templateResult.TrainingPlanId &&
                    x.UserId == userId);
            Assert.Equal(2, storedTemplate.Exercises.Count);
            Assert.All(storedTemplate.Exercises, storedExercise =>
            {
                Assert.Equal(supersetGroupId, storedExercise.SupersetGroupId);
                Assert.Equal(WorkoutPlanner.Web.Models.ExerciseStatus.NotCompleted, storedExercise.Status);
                Assert.All(storedExercise.Sets, set => Assert.False(set.Completed));
            });
            Assert.Equal(daysBefore, await db.WorkoutDays.CountAsync(x => x.UserId == userId));
            Assert.Equal(workoutProgressBefore + 1, await db.ProgressSnapshots.CountAsync(x => x.UserId == userId));
            Assert.Equal(exerciseProgressBefore + 2, await db.ExerciseProgressSnapshots.CountAsync(x => x.UserId == userId));
        }

        var progress = await client.GetFromJsonAsync<WorkoutProgressApiResponse>(
            $"/api/v1/progress/workouts/{templateResult.TrainingPlanId}");
        Assert.NotNull(progress);
        Assert.Single(progress.Snapshots);
        Assert.Single(progress.Chart);
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

    [Fact]
    public async Task WatchPairing_RotatesRefreshTokenAndRevocationStopsAccess()
    {
        using var factory = new GymPlannerApiFactory();
        using var owner = CreateClient(factory);
        using var watch = CreateClient(factory);

        using var anonymousCode = await watch.PostAsync(
            "/api/watch/pairing-codes",
            null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCode.StatusCode);

        using var registration = await owner.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("watch-owner@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var mobileTokens = await LoginAsync(
            owner,
            "watch-owner@example.test",
            "password1",
            "Owner phone");
        SetBearer(owner, mobileTokens.AccessToken);

        using var codeResponse = await owner.PostAsync(
            "/api/watch/pairing-codes",
            null);
        Assert.Equal(HttpStatusCode.Created, codeResponse.StatusCode);
        var pairingCode = await codeResponse.Content
            .ReadFromJsonAsync<CreateWatchPairingCodeResponse>();
        Assert.NotNull(pairingCode);
        Assert.Matches("^[0-9]{6}$", pairingCode.Code);
        Assert.True(pairingCode.ExpiresAtUtc > DateTime.UtcNow);

        var pairRequest = new PairWatchRequest(
            pairingCode.Code,
            "stable-watch-installation",
            "Galaxy Watch",
            "SM-R960",
            "1.0.0");
        using var paired = await watch.PostAsJsonAsync(
            "/api/watch/pair",
            pairRequest);
        Assert.Equal(HttpStatusCode.OK, paired.StatusCode);
        var tokens = await paired.Content.ReadFromJsonAsync<WatchTokenResponse>();
        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));

        using var replay = await watch.PostAsJsonAsync(
            "/api/watch/pair",
            pairRequest);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var storedCode = await db.WatchPairingCodes.SingleAsync();
            var storedDevice = await db.WatchDevices.SingleAsync();
            Assert.NotEqual(pairingCode.Code, storedCode.CodeHash);
            Assert.NotEqual(tokens.RefreshToken, storedDevice.RefreshTokenHash);
            Assert.NotNull(storedCode.UsedAtUtc);
        }

        watch.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var watchCannotManage = await watch.GetAsync("/api/watch/devices");
        Assert.Equal(HttpStatusCode.Forbidden, watchCannotManage.StatusCode);
        watch.DefaultRequestHeaders.Authorization = null;

        var firstRefreshTask = watch.PostAsJsonAsync(
            "/api/watch/token/refresh",
            new RefreshWatchTokenRequest(tokens.RefreshToken));
        var secondRefreshTask = watch.PostAsJsonAsync(
            "/api/watch/token/refresh",
            new RefreshWatchTokenRequest(tokens.RefreshToken));
        var refreshResponses = await Task.WhenAll(firstRefreshTask, secondRefreshTask);
        using var firstRefresh = refreshResponses[0];
        using var secondRefresh = refreshResponses[1];
        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.Unauthorized],
            refreshResponses.Select(x => x.StatusCode).Order().ToArray());
        var successfulRefresh = refreshResponses.Single(x => x.StatusCode == HttpStatusCode.OK);
        var rotated = await successfulRefresh.Content.ReadFromJsonAsync<WatchTokenResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(tokens.RefreshToken, rotated.RefreshToken);

        var devices = await owner.GetFromJsonAsync<List<WatchDeviceResponse>>(
            "/api/watch/devices");
        var device = Assert.Single(devices!);
        Assert.Equal("stable-watch-installation", device.DeviceId);
        Assert.False(device.IsRevoked);

        using var revoked = await owner.DeleteAsync(
            "/api/watch/devices/stable-watch-installation");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        using var revokedRefresh = await watch.PostAsJsonAsync(
            "/api/watch/token/refresh",
            new RefreshWatchTokenRequest(rotated.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, revokedRefresh.StatusCode);
    }

    [Fact]
    public async Task WatchManagement_IsUserScopedAndExpiredCodesAreRejected()
    {
        using var factory = new GymPlannerApiFactory();
        using var owner = CreateClient(factory);
        using var other = CreateClient(factory);
        using var watch = CreateClient(factory);

        foreach (var (client, email) in new[]
                 {
                     (owner, "watch-scope-owner@example.test"),
                     (other, "watch-scope-other@example.test")
                 })
        {
            using var registration = await client.PostAsJsonAsync(
                "/api/v1/auth/register",
                new MobileRegisterRequest(email, "password1"));
            Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
            var tokens = await LoginAsync(client, email, "password1", "Phone");
            SetBearer(client, tokens.AccessToken);
        }

        using var codeResponse = await owner.PostAsync(
            "/api/watch/pairing-codes",
            null);
        var code = await codeResponse.Content
            .ReadFromJsonAsync<CreateWatchPairingCodeResponse>();
        Assert.NotNull(code);

        using var replacementResponse = await owner.PostAsync(
            "/api/watch/pairing-codes",
            null);
        var replacementCode = await replacementResponse.Content
            .ReadFromJsonAsync<CreateWatchPairingCodeResponse>();
        Assert.NotNull(replacementCode);
        using var invalidated = await watch.PostAsJsonAsync(
            "/api/watch/pair",
            new PairWatchRequest(
                code.Code,
                "invalidated-code-device",
                "Invalidated",
                null,
                null));
        Assert.Equal(HttpStatusCode.BadRequest, invalidated.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var stored = await db.WatchPairingCodes.SingleAsync(x => x.UsedAtUtc == null);
            stored.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-15);
            stored.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        using var expired = await watch.PostAsJsonAsync(
            "/api/watch/pair",
            new PairWatchRequest(
                replacementCode.Code,
                "expired-code-device",
                "Expired",
                null,
                null));
        Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);

        var otherDevices = await other.GetFromJsonAsync<List<WatchDeviceResponse>>(
            "/api/watch/devices");
        Assert.NotNull(otherDevices);
        Assert.Empty(otherDevices);
        using var crossUserDelete = await other.DeleteAsync(
            "/api/watch/devices/expired-code-device");
        Assert.Equal(HttpStatusCode.NotFound, crossUserDelete.StatusCode);
    }

    [Fact]
    public async Task WatchRefresh_IsInvalidatedByPasswordSecurityStampChange()
    {
        using var factory = new GymPlannerApiFactory();
        using var owner = CreateClient(factory);
        using var watch = CreateClient(factory);
        using var registration = await owner.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("watch-password@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var mobileTokens = await LoginAsync(
            owner,
            "watch-password@example.test",
            "password1",
            "Phone");
        SetBearer(owner, mobileTokens.AccessToken);

        using var codeResponse = await owner.PostAsync("/api/watch/pairing-codes", null);
        var code = await codeResponse.Content.ReadFromJsonAsync<CreateWatchPairingCodeResponse>();
        Assert.NotNull(code);
        using var paired = await watch.PostAsJsonAsync(
            "/api/watch/pair",
            new PairWatchRequest(code.Code, "password-watch", "Watch", null, null));
        var tokens = await paired.Content.ReadFromJsonAsync<WatchTokenResponse>();
        Assert.NotNull(tokens);

        using var changed = await owner.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new ChangePasswordApiRequest("password1", "password2"));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        using var refresh = await watch.PostAsJsonAsync(
            "/api/watch/token/refresh",
            new RefreshWatchTokenRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task WatchPairingEndpoint_RateLimitsAnonymousAttempts()
    {
        using var factory = new GymPlannerApiFactory();
        using var client = CreateClient(factory);
        HttpStatusCode lastStatus = 0;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            using var response = await client.PostAsJsonAsync(
                "/api/watch/pair",
                new PairWatchRequest(
                    "000000",
                    $"rate-limit-device-{attempt}",
                    "Rate limit test",
                    null,
                    null));
            lastStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }

    [Fact]
    public async Task WatchWorkoutApi_ReturnsCurrentSetCompletesAndReplaysIdempotently()
    {
        using var factory = new GymPlannerApiFactory();
        using var owner = CreateClient(factory);
        using var watch = CreateClient(factory);
        await RegisterLoginAndPairWatchAsync(
            owner,
            watch,
            "watch-workout@example.test",
            "workout-watch");

        int firstSetId;
        int secondSetId;
        int firstExerciseId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var userId = await db.Users
                .Where(x => x.Email == "watch-workout@example.test")
                .Select(x => x.Id)
                .SingleAsync();
            var plan = new WorkoutPlanner.Web.Models.TrainingPlan
            {
                UserId = userId,
                WorkoutName = "Watch workout",
                Date = DateTime.Today,
                Exercises =
                [
                    new WorkoutPlanner.Web.Models.Exercise
                    {
                        UserId = userId,
                        Name = "Squat",
                        WorkoutName = "Watch workout",
                        Sets =
                        [
                            new WorkoutPlanner.Web.Models.ExerciseTemplateSet
                            {
                                SetNumber = 2,
                                Weight = 80,
                                Repetitions = 6
                            },
                            new WorkoutPlanner.Web.Models.ExerciseTemplateSet
                            {
                                SetNumber = 1,
                                Weight = 70,
                                Repetitions = 8
                            }
                        ]
                    },
                    new WorkoutPlanner.Web.Models.Exercise
                    {
                        UserId = userId,
                        Name = "Press",
                        WorkoutName = "Watch workout",
                        Sets =
                        [
                            new WorkoutPlanner.Web.Models.ExerciseTemplateSet
                            {
                                SetNumber = 1,
                                Weight = 30,
                                Repetitions = 10
                            }
                        ]
                    }
                ]
            };
            db.TrainingPlans.Add(plan);
            await db.SaveChangesAsync();
            db.WorkoutDays.Add(new WorkoutPlanner.Web.Models.WorkoutDay
            {
                UserId = userId,
                Date = DateTime.Today,
                TrainingPlanId = plan.Id
            });
            await db.SaveChangesAsync();
            firstExerciseId = plan.Exercises[0].Id;
            firstSetId = plan.Exercises[0].Sets.Single(x => x.SetNumber == 1).Id;
            secondSetId = plan.Exercises[0].Sets.Single(x => x.SetNumber == 2).Id;
        }

        var active = await watch.GetFromJsonAsync<WatchActiveWorkoutResponse>(
            "/api/watch/workouts/active");
        Assert.NotNull(active);
        Assert.Null(active.StartedAtUtc);
        Assert.Equal(firstExerciseId, active.CurrentExerciseId);
        Assert.Equal(firstSetId, active.CurrentSetId);
        Assert.Equal([1, 2], active.Exercises[0].Sets.Select(x => x.SetNumber));
        Assert.Equal([70d, 80d], active.Exercises[0].Sets.Select(x => x.Weight));

        var operationId = Guid.NewGuid();
        var request = new CompleteWatchSetRequest(
            operationId,
            DateTime.UtcNow,
            ClientVersion: 0);
        using var completedResponse = await watch.PostAsJsonAsync(
            $"/api/watch/sets/{firstSetId}/complete",
            request);
        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);
        var completed = await completedResponse.Content
            .ReadFromJsonAsync<CompleteWatchSetResponse>();
        Assert.NotNull(completed);
        Assert.True(completed.Set.IsCompleted);
        Assert.Equal(1, completed.Set.Version);
        Assert.Equal(secondSetId, completed.CurrentSetId);

        using var replayResponse = await watch.PostAsJsonAsync(
            $"/api/watch/sets/{firstSetId}/complete",
            request);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var replay = await replayResponse.Content
            .ReadFromJsonAsync<CompleteWatchSetResponse>();
        Assert.Equal(completed, replay);

        using var reusedForAnotherSet = await watch.PostAsJsonAsync(
            $"/api/watch/sets/{secondSetId}/complete",
            request);
        Assert.Equal(HttpStatusCode.Conflict, reusedForAnotherSet.StatusCode);

        using var conflictResponse = await watch.PostAsJsonAsync(
            $"/api/watch/sets/{firstSetId}/complete",
            new CompleteWatchSetRequest(Guid.NewGuid(), DateTime.UtcNow, 0));
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        var conflict = await conflictResponse.Content
            .ReadFromJsonAsync<WatchSetConflictResponse>();
        Assert.NotNull(conflict);
        Assert.Equal("WORKOUT_SET_CONFLICT", conflict.Code);
        Assert.Equal(1, conflict.Set!.Version);
        Assert.True(conflict.Set.IsCompleted);
        Assert.Equal(secondSetId, conflict.CurrentSetId);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationFactory = verificationScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
        await using var verificationDb = await verificationFactory.CreateDbContextAsync();
        var storedSet = await verificationDb.ExerciseTemplateSets
            .AsNoTracking()
            .SingleAsync(x => x.Id == firstSetId);
        Assert.True(storedSet.Completed);
        Assert.Equal(1, storedSet.Version);
        Assert.Equal(
            1,
            await verificationDb.WatchSyncOperations.CountAsync(x =>
                x.OperationId == operationId));
    }

    [Fact]
    public async Task WatchWorkoutApi_ProtectsOwnershipFinishedStateAndRevocation()
    {
        using var factory = new GymPlannerApiFactory();
        using var owner = CreateClient(factory);
        using var other = CreateClient(factory);
        using var watch = CreateClient(factory);
        await RegisterLoginAndPairWatchAsync(
            owner,
            watch,
            "watch-security@example.test",
            "security-watch");

        using var noWorkout = await watch.GetAsync("/api/watch/workouts/active");
        Assert.Equal(HttpStatusCode.NotFound, noWorkout.StatusCode);

        using var otherRegistration = await other.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("watch-other@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, otherRegistration.StatusCode);
        int foreignSetId;
        int finishedSetId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var ownerId = await db.Users
                .Where(x => x.Email == "watch-security@example.test")
                .Select(x => x.Id)
                .SingleAsync();
            var otherId = await db.Users
                .Where(x => x.Email == "watch-other@example.test")
                .Select(x => x.Id)
                .SingleAsync();
            var finishedPlan = CreateWatchTestPlan(ownerId, "Finished");
            var foreignPlan = CreateWatchTestPlan(otherId, "Foreign");
            db.TrainingPlans.AddRange(finishedPlan, foreignPlan);
            await db.SaveChangesAsync();
            db.WorkoutDays.AddRange(
                new WorkoutPlanner.Web.Models.WorkoutDay
                {
                    UserId = ownerId,
                    Date = DateTime.Today,
                    TrainingPlanId = finishedPlan.Id,
                    IsCompleted = true
                },
                new WorkoutPlanner.Web.Models.WorkoutDay
                {
                    UserId = otherId,
                    Date = DateTime.Today,
                    TrainingPlanId = foreignPlan.Id
                });
            await db.SaveChangesAsync();
            finishedSetId = finishedPlan.Exercises[0].Sets[0].Id;
            foreignSetId = foreignPlan.Exercises[0].Sets[0].Id;
        }

        using var finished = await watch.GetAsync("/api/watch/workouts/active");
        Assert.Equal(HttpStatusCode.Gone, finished.StatusCode);
        using var finishedCompletion = await watch.PostAsJsonAsync(
            $"/api/watch/sets/{finishedSetId}/complete",
            new CompleteWatchSetRequest(Guid.NewGuid(), DateTime.UtcNow, 0));
        Assert.Equal(HttpStatusCode.Gone, finishedCompletion.StatusCode);
        using var foreignCompletion = await watch.PostAsJsonAsync(
            $"/api/watch/sets/{foreignSetId}/complete",
            new CompleteWatchSetRequest(Guid.NewGuid(), DateTime.UtcNow, 0));
        Assert.Equal(HttpStatusCode.NotFound, foreignCompletion.StatusCode);

        using var revoked = await owner.DeleteAsync(
            "/api/watch/devices/security-watch");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        using var afterRevoke = await watch.GetAsync("/api/watch/workouts/active");
        Assert.Equal(HttpStatusCode.Forbidden, afterRevoke.StatusCode);
    }

    private static async Task RegisterLoginAndPairWatchAsync(
        HttpClient owner,
        HttpClient watch,
        string email,
        string deviceId)
    {
        using var registration = await owner.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest(email, "password1"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var mobileTokens = await LoginAsync(owner, email, "password1", "Owner phone");
        SetBearer(owner, mobileTokens.AccessToken);
        using var codeResponse = await owner.PostAsync("/api/watch/pairing-codes", null);
        var code = await codeResponse.Content
            .ReadFromJsonAsync<CreateWatchPairingCodeResponse>();
        Assert.NotNull(code);
        using var pairResponse = await watch.PostAsJsonAsync(
            "/api/watch/pair",
            new PairWatchRequest(code.Code, deviceId, "Test watch", null, "1.0"));
        Assert.Equal(HttpStatusCode.OK, pairResponse.StatusCode);
        var tokens = await pairResponse.Content.ReadFromJsonAsync<WatchTokenResponse>();
        Assert.NotNull(tokens);
        SetBearer(watch, tokens.AccessToken);
    }

    private static WorkoutPlanner.Web.Models.TrainingPlan CreateWatchTestPlan(
        string userId,
        string name) =>
        new()
        {
            UserId = userId,
            WorkoutName = name,
            Date = DateTime.Today,
            Exercises =
            [
                new WorkoutPlanner.Web.Models.Exercise
                {
                    UserId = userId,
                    Name = "Exercise",
                    WorkoutName = name,
                    Sets =
                    [
                        new WorkoutPlanner.Web.Models.ExerciseTemplateSet
                        {
                            SetNumber = 1,
                            Weight = 50,
                            Repetitions = 8
                        }
                    ]
                }
            ]
        };

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    private static async Task<MobileTokenResponse> LoginAsync(
        HttpClient client,
        string email,
        string password,
        string deviceName)
    {
        using var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new MobileLoginRequest(email, password, deviceName));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<MobileTokenResponse>();
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

    [Fact]
    public async Task TelegramReply_CreatesUserInboxMessage_AndCanBeRead()
    {
        using var factory = new GymPlannerApiFactory();
        using var client = CreateClient(factory);
        using var registration = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("inbox@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var tokens = await LoginAsync(client, "inbox@example.test", "password1", "Inbox phone");
        SetBearer(client, tokens.AccessToken);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var userId = await db.Users.Where(x => x.Email == "inbox@example.test").Select(x => x.Id).SingleAsync();
            db.SupportTickets.Add(new SupportTicket
            {
                UserId = userId,
                TicketNumber = "GP-20260820-000001",
                Message = "Не работает вход в приложение.",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                TelegramMessageId = 501
            });
            await db.SaveChangesAsync();
        }

        const string payload = "{\"update_id\":1,\"message\":{\"message_id\":502,\"date\":1787220000,\"chat\":{\"id\":1198730360},\"text\":\"Мы проверили проблему. Попробуйте обновить приложение.\",\"reply_to_message\":{\"message_id\":501}}}";
        using var webhook = new HttpRequestMessage(
            HttpMethod.Post,
            "/integrations/telegram/support-webhook")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };
        webhook.Headers.Add("X-Telegram-Bot-Api-Secret-Token", "test-webhook-secret");
        using var delivered = await client.SendAsync(webhook);
        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);

        using var duplicate = new HttpRequestMessage(
            HttpMethod.Post,
            "/integrations/telegram/support-webhook")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };
        duplicate.Headers.Add("X-Telegram-Bot-Api-Secret-Token", "test-webhook-secret");
        using var duplicateDelivered = await client.SendAsync(duplicate);
        Assert.Equal(HttpStatusCode.OK, duplicateDelivered.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var supportMessage = await db.SupportMessages.SingleAsync();
            Assert.Equal(SupportMessageSenderType.Support, supportMessage.SenderType);
            Assert.Equal(
                "Мы проверили проблему. Попробуйте обновить приложение.",
                supportMessage.Message);
        }

        var inbox = await client.GetFromJsonAsync<InboxMessagesResponse>("/api/v1/inbox/messages");
        Assert.NotNull(inbox);
        var message = Assert.Single(inbox.Messages);
        Assert.Equal("SupportReply", message.Type);
        Assert.Equal("GP-20260820-000001", message.SupportTicketNumber);
        Assert.Equal(1, inbox.UnreadCount);

        using var markedRead = await client.PostAsync($"/api/v1/inbox/messages/{message.Id}/read", null);
        Assert.Equal(HttpStatusCode.OK, markedRead.StatusCode);
        var count = await markedRead.Content.ReadFromJsonAsync<InboxUnreadCountResponse>();
        Assert.NotNull(count);
        Assert.Equal(0, count.UnreadCount);

        using var deleted = await client.DeleteAsync($"/api/v1/inbox/messages/{message.Id}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        var afterDelete = await client.GetFromJsonAsync<InboxMessagesResponse>("/api/v1/inbox/messages");
        Assert.NotNull(afterDelete);
        Assert.Empty(afterDelete.Messages);
    }

    [Fact]
    public async Task InboxMessageEndpoint_ReturnsOnlyCurrentUsersMessage()
    {
        using var factory = new GymPlannerApiFactory();
        using var ownerClient = CreateClient(factory);
        using var otherClient = CreateClient(factory);

        using var ownerRegistration = await ownerClient.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("inbox-owner@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, ownerRegistration.StatusCode);
        using var otherRegistration = await otherClient.PostAsJsonAsync(
            "/api/v1/auth/register",
            new MobileRegisterRequest("inbox-other@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, otherRegistration.StatusCode);

        var ownerTokens = await LoginAsync(
            ownerClient,
            "inbox-owner@example.test",
            "password1",
            "Owner phone");
        var otherTokens = await LoginAsync(
            otherClient,
            "inbox-other@example.test",
            "password1",
            "Other phone");
        SetBearer(ownerClient, ownerTokens.AccessToken);
        SetBearer(otherClient, otherTokens.AccessToken);

        long messageId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<WorkoutDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            var ownerId = await db.Users
                .Where(x => x.Email == "inbox-owner@example.test")
                .Select(x => x.Id)
                .SingleAsync();
            var message = new InboxMessage
            {
                UserId = ownerId,
                Type = InboxMessageType.SupportReply,
                Title = "Ответ поддержки",
                Body = "PRIVATE_SENTINEL_direct_message_owner_only",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.InboxMessages.Add(message);
            await db.SaveChangesAsync();
            messageId = message.Id;
        }

        using var forbiddenLookup = await otherClient.GetAsync(
            $"/api/v1/inbox/messages/{messageId}");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenLookup.StatusCode);
        Assert.DoesNotContain(
            "PRIVATE_SENTINEL_direct_message_owner_only",
            await forbiddenLookup.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        using var ownerLookup = await ownerClient.GetAsync(
            $"/api/v1/inbox/messages/{messageId}");
        Assert.Equal(HttpStatusCode.OK, ownerLookup.StatusCode);
        var ownedMessage = await ownerLookup.Content
            .ReadFromJsonAsync<InboxMessageResponse>();
        Assert.NotNull(ownedMessage);
        Assert.Equal(messageId, ownedMessage.Id);
        Assert.Equal("PRIVATE_SENTINEL_direct_message_owner_only", ownedMessage.Body);
    }

    [Fact]
    public async Task AdminPublicationsEndpoint_RejectsAnonymousAndUser_ButAllowsAdmin()
    {
        using var factory = new GymPlannerApiFactory();
        using var anonymous = CreateClient(factory);
        var request = new
        {
            Type = "News",
            Title = "Новая функция",
            Body = "В GPlanner появились таймеры отдыха.",
            PublishedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        using var anonymousResponse = await anonymous.PostAsJsonAsync("/api/admin/publications", request);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        using var anonymousRead = await anonymous.GetAsync("/api/admin/publications");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousRead.StatusCode);

        using var userClient = CreateClient(factory);
        using var registration = await userClient.PostAsJsonAsync(
            "/api/v1/auth/register", new MobileRegisterRequest("admin-role@example.test", "password1"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var userTokens = await LoginAsync(userClient, "admin-role@example.test", "password1", "User session");
        SetBearer(userClient, userTokens.AccessToken);
        using var forbidden = await userClient.PostAsJsonAsync("/api/admin/publications", request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var forbiddenRead = await userClient.GetAsync("/api/admin/publications");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRead.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = await users.FindByEmailAsync("admin-role@example.test");
            Assert.NotNull(user);
            var result = await users.AddToRoleAsync(user, ApplicationRoles.Admin);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
        }

        using var adminClient = CreateClient(factory);
        var adminTokens = await LoginAsync(adminClient, "admin-role@example.test", "password1", "Admin session");
        SetBearer(adminClient, adminTokens.AccessToken);
        using var created = await adminClient.PostAsJsonAsync("/api/admin/publications", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var allowedRead = await adminClient.GetAsync("/api/admin/publications");
        Assert.Equal(HttpStatusCode.OK, allowedRead.StatusCode);

        var inbox = await adminClient.GetFromJsonAsync<InboxMessagesResponse>("/api/v1/inbox/messages");
        Assert.NotNull(inbox);
        var publication = Assert.Single(inbox.Messages);
        Assert.True(publication.IsPublication);
        Assert.Equal(1, inbox.UnreadCount);
        using var read = await adminClient.PostAsync($"/api/v1/inbox/publications/{publication.Id}/read", null);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var deleted = await adminClient.DeleteAsync($"/api/v1/inbox/publications/{publication.Id}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        var afterDelete = await adminClient.GetFromJsonAsync<InboxMessagesResponse>("/api/v1/inbox/messages");
        Assert.NotNull(afterDelete);
        Assert.Empty(afterDelete.Messages);

        using var logout = await adminClient.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var revokedRead = await adminClient.GetAsync("/api/admin/publications");
        Assert.Equal(HttpStatusCode.Forbidden, revokedRead.StatusCode);
    }

    private static MultipartFormDataContent CreateSupportContent(
        string message,
        byte[]? screenshot = null,
        string contentType = "image/png",
        string? appVersion = null,
        string? platform = null,
        string? osVersion = null,
        string? deviceModel = null)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(message), "message" }
        };
        AddOptional(content, "appVersion", appVersion);
        AddOptional(content, "platform", platform);
        AddOptional(content, "osVersion", osVersion);
        AddOptional(content, "deviceModel", deviceModel);
        if (screenshot is not null)
        {
            var file = new ByteArrayContent(screenshot);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(file, "screenshot", "client-name.png");
        }
        return content;
    }

    private static void AddOptional(
        MultipartFormDataContent content,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            content.Add(new StringContent(value), name);
    }

    private sealed class GymPlannerApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _environmentName;
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            "GymPlanner.Tests",
            $"api-{Guid.NewGuid():N}.db");

        private readonly ISupportNotificationService? _supportNotification;

        public GymPlannerApiFactory(
            string environmentName = "Testing",
            ISupportNotificationService? supportNotification = null)
        {
            _environmentName = environmentName;
            _supportNotification = supportNotification;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            builder.UseEnvironment(_environmentName);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:WorkoutDatabase"] = $"Data Source={_databasePath}",
                    ["TelegramSupport:ChatId"] = "1198730360",
                    ["TelegramSupport:WebhookSecret"] = "test-webhook-secret"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection()
                    .UseEphemeralDataProtectionProvider();
                if (_supportNotification is not null)
                {
                    services.RemoveAll<ISupportNotificationService>();
                    services.AddSingleton(_supportNotification);
                }
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

    private sealed class FailingSupportNotificationService :
        ISupportNotificationService
    {
        public int CallCount { get; private set; }

        public string? LastTicketNumber { get; private set; }

        public Task<SupportNotificationResult> NotifyTicketCreatedAsync(
            SupportTicketNotification notification,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastTicketNumber = notification.TicketNumber;
            return Task.FromResult(SupportNotificationResult.Failed);
        }
    }
}
