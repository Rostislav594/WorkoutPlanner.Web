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
