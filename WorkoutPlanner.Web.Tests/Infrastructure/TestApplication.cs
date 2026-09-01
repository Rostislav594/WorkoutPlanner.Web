using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Services;
using WorkoutPlanner.Web.Services.Activity;
using WorkoutPlanner.Web.Services.Admin;
using WorkoutPlanner.Web.Services.Auth;
using WorkoutPlanner.Web.Services.Support;
using WorkoutPlanner.Web.Services.Push;

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

    public static async Task<TestApplication> CreateAsync(
        IPushNotificationService? pushNotificationService = null)
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
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(connection);
        services.AddSingleton(authenticationStateProvider);
        services.AddSingleton<AuthenticationStateProvider>(
            authenticationStateProvider);
        services.AddSingleton<IWebHostEnvironment>(environment);
        services.AddHttpContextAccessor();
        services.AddDbContextFactory<WorkoutDbContext>((provider, options) =>
            options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
        services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<WorkoutDbContext>();
        services.AddScoped<CurrentUserService>();
        services.AddSingleton(
            pushNotificationService ?? NoopPushNotificationService.Instance);
        services.AddScoped<AccountDeletionService>();
        services.AddScoped<StarterPlanService>();
        services.AddScoped<TrainingPlanService>();
        services.AddScoped<ExerciseService>();
        services.AddScoped<WorkoutDayService>();
        services.AddScoped<HistoryService>();
        services.AddScoped<WorkoutCompletionService>();
        services.AddScoped<PushDeviceRegistrationService>();
        services.AddScoped<IPushDeviceRegistrationService>(sp =>
            sp.GetRequiredService<PushDeviceRegistrationService>());
        services.AddScoped<IPushDeviceStore>(sp =>
            sp.GetRequiredService<PushDeviceRegistrationService>());
        services.AddScoped<ExerciseIndexService>();
        services.AddScoped<ProgressService>();
        services.Configure<UserActivityOptions>(_ => { });
        services.AddSingleton<WorkoutPlanner.Web.Application.Abstractions.ISupportScreenshotStorage, SupportScreenshotStorage>();
        services.AddGymPlannerAdminOperations(new ConfigurationBuilder().Build());
        services.AddScoped<WorkoutPlanner.Web.Application.Abstractions.IInboxService, InboxService>();
        services.AddScoped<WorkoutPlanner.Web.Application.Abstractions.IAdminPublicationService, AdminPublicationService>();
        services.AddSingleton<WorkoutPlanner.Web.Application.Abstractions.IInboxPublicationImageStorage, PublicationImageStorage>();

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

    private sealed class NoopPushNotificationService : IPushNotificationService
    {
        public static NoopPushNotificationService Instance { get; } = new();

        public Task NotifyInboxMessageAsync(
            string userId,
            WorkoutPlanner.Api.Contracts.PushNotificationType type,
            long inboxMessageId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
