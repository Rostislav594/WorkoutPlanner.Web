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
using WorkoutPlanner.Web.Api.Contracts;
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
