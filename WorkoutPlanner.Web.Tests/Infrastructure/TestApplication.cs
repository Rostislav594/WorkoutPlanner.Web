using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services;
using WorkoutPlanner.Web.Services.Auth;

namespace WorkoutPlanner.Web.Tests.Infrastructure;

internal sealed class TestApplication : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;

    private TestApplication(
        SqliteConnection connection,
        ServiceProvider services,
        TestAuthenticationStateProvider authenticationStateProvider,
        string contentRootPath,
        string webRootPath)
    {
        _connection = connection;
        _services = services;
        AuthenticationStateProvider = authenticationStateProvider;
        ContentRootPath = contentRootPath;
        WebRootPath = webRootPath;
    }

    public TestAuthenticationStateProvider AuthenticationStateProvider
    {
        get;
    }

    public string ContentRootPath { get; }

    public string WebRootPath { get; }

    public static async Task<TestApplication> CreateAsync()
    {
        var contentRootPath = Path.Combine(
            Path.GetTempPath(),
            "GymPlanner.Tests",
            Guid.NewGuid().ToString("N"));
        var webRootPath = Path.Combine(contentRootPath, "wwwroot");

        Directory.CreateDirectory(contentRootPath);
        Directory.CreateDirectory(webRootPath);

        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var authenticationStateProvider =
            new TestAuthenticationStateProvider();
        var environment = new TestWebHostEnvironment
        {
            ApplicationName = "WorkoutPlanner.Web.Tests",
            EnvironmentName = "Testing",
            ContentRootPath = contentRootPath,
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath),
            WebRootPath = webRootPath,
            WebRootFileProvider = new PhysicalFileProvider(webRootPath)
        };

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(connection);
        services.AddSingleton(authenticationStateProvider);
        services.AddSingleton<AuthenticationStateProvider>(
            authenticationStateProvider);
        services.AddSingleton<IWebHostEnvironment>(environment);
        services.AddDbContext<WorkoutDbContext>((provider, options) =>
            options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
        services.AddIdentityCore<IdentityUser>()
            .AddEntityFrameworkStores<WorkoutDbContext>();
        services.AddScoped<CurrentUserService>();
        services.AddScoped<AccountDeletionService>();
        services.AddScoped<StarterPlanService>();
        services.AddScoped<TrainingPlanService>();
        services.AddScoped<ExerciseService>();
        services.AddScoped<WorkoutDayService>();
        services.AddScoped<HistoryService>();
        services.AddScoped<SecondaryMuscleCoefficientService>();
        services.AddScoped<ExerciseIndexService>();
        services.AddScoped<ProgressService>();

        var serviceProvider = services.BuildServiceProvider();

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<WorkoutDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        return new TestApplication(
            connection,
            serviceProvider,
            authenticationStateProvider,
            contentRootPath,
            webRootPath);
    }

    public AsyncServiceScope CreateScope()
    {
        return _services.CreateAsyncScope();
    }

    public async Task<IdentityUser> CreateUserAsync(string userId)
    {
        await using var scope = CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser
        {
            Id = userId,
            UserName = $"{userId}@example.test",
            Email = $"{userId}@example.test"
        };

        var result = await userManager.CreateAsync(user);

        Assert.True(
            result.Succeeded,
            string.Join("; ", result.Errors.Select(x => x.Description)));

        return user;
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();

        if (Directory.Exists(ContentRootPath))
        {
            Directory.Delete(ContentRootPath, recursive: true);
        }
    }
}
