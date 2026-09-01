using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;
using WorkoutPlanner.Web.Components;
using WorkoutPlanner.Web.Api;
using WorkoutPlanner.Web.Api.Security;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services;
using WorkoutPlanner.Web.Services.Activity;
using WorkoutPlanner.Web.Services.Admin;
using WorkoutPlanner.Web.Services.Auth;
using WorkoutPlanner.Web.Services.Push;
using WorkoutPlanner.Web.Services.Support;
using WorkoutPlanner.Web.Services.WelcomeGuide;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddDbContextFactory<WorkoutDbContext>(
    options =>
        options.UseSqlite(
            builder.Configuration.GetConnectionString("WorkoutDatabase")
            ?? "Data Source=workoutplanner.db"));

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(
        new DirectoryInfo(
            Path.Combine(
                builder.Environment.ContentRootPath,
                "App_Data",
                "DataProtectionKeys")));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        ApplicationRoles.AdminPolicy,
        policy => policy.RequireAuthenticatedUser().RequireRole(ApplicationRoles.Admin));
    options.AddPolicy(
        ApplicationRoles.AdminApiPolicy,
        policy =>
        {
            policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireRole(ApplicationRoles.Admin);
            policy.AddRequirements(new MobileApiSessionRequirement());
        });
    options.AddPolicy(
        MobileApiAuthorization.PolicyName,
        policy =>
        {
            policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme);
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new MobileApiSessionRequirement());
        });
});
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(
        SupportApiEndpoints.RateLimitPolicyName,
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            context.Connection.RemoteIpAddress?.ToString() ??
            "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddIdentityApiEndpoints<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<WorkoutDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";
});
builder.Services.Configure<BearerTokenOptions>(
    IdentityConstants.BearerScheme,
    options =>
    {
        options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
        options.RefreshTokenExpiration = TimeSpan.FromDays(7);
    });
builder.Services.Configure<AuthenticationOptions>(options =>
{
    options.DefaultChallengeScheme =
        IdentityConstants.ApplicationScheme;
});
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<MobileSessionService>();
builder.Services.AddScoped<
    Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    MobileApiSessionAuthorizationHandler>();
builder.Services.AddScoped<AccountDeletionService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IProfileService, UserProfileService>();
builder.Services.AddScoped<IHistoryService, HistoryService>();
builder.Services.AddScoped<IWorkoutCompletionService, WorkoutCompletionService>();
builder.Services.AddScoped<IExerciseService, ExerciseService>();
builder.Services.AddScoped<IExercisePhotoService, ExercisePhotoService>();
builder.Services.AddScoped<
    IExercisePhotoApplicationService,
    ExercisePhotoApplicationService>();
builder.Services.AddScoped<ITrainingPlanService, TrainingPlanService>();
builder.Services.AddScoped<IStarterPlanService, StarterPlanService>();
builder.Services.AddScoped<ITodayWorkoutService, TodayWorkoutService>();
builder.Services.AddScoped<IWorkoutDayService, WorkoutDayService>();
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<IExerciseDefinitionService, ExerciseDefinitionService>();
builder.Services.AddScoped<ExerciseIndexService>();
builder.Services.AddScoped<IWelcomeGuideCompletionStore, IdentityWelcomeGuideCompletionStore>();
builder.Services.AddScoped<IWelcomeGuideStateService, WelcomeGuideStateService>();
builder.Services.Configure<TelegramSupportOptions>(
    builder.Configuration.GetSection(TelegramSupportOptions.SectionName));
builder.Services.AddSingleton<ISupportScreenshotStorage, SupportScreenshotStorage>();
builder.Services.AddSingleton<ISupportNotificationService, TelegramSupportNotificationService>();
builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();
builder.Services.AddSingleton<IPushNotificationTemplateProvider,
    PushNotificationTemplateProvider>();
builder.Services.AddScoped<PushDeviceRegistrationService>();
builder.Services.AddScoped<IPushDeviceRegistrationService>(sp =>
    sp.GetRequiredService<PushDeviceRegistrationService>());
builder.Services.AddScoped<IPushDeviceStore>(sp => sp.GetRequiredService<PushDeviceRegistrationService>());
builder.Services.AddScoped<IRemotePushProvider>(sp =>
    sp.GetRequiredService<IConfiguration>().GetValue<bool>("Push:Enabled")
        ? sp.GetRequiredService<FirebaseRemotePushProvider>()
        : sp.GetRequiredService<DisabledRemotePushProvider>());
builder.Services.AddScoped<DisabledRemotePushProvider>();
if (builder.Configuration.GetValue<bool>("Push:Enabled"))
{
    builder.Services.AddFirebasePush(builder.Configuration);
    builder.Services.AddScoped<FirebaseRemotePushProvider>();
}
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<IInboxService, InboxService>();
builder.Services.Configure<UserActivityOptions>(
    builder.Configuration.GetSection(UserActivityOptions.SectionName));
builder.Services.AddScoped<UserActivityService>();
builder.Services.AddScoped<IAdminPublicationService, AdminPublicationService>();
builder.Services.AddSingleton<IInboxPublicationImageStorage, PublicationImageStorage>();
builder.Services.AddGymPlannerAdminOperations(builder.Configuration);


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/api"),
        web => web.UseExceptionHandler("/Error", createScopeForErrors: true));
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    api =>
    {
        api.UseExceptionHandler();
        api.UseStatusCodePages();
    });
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    web => web.UseStatusCodePagesWithReExecute(
        "/not-found",
        createScopeForStatusCodePages: true));

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<UserActivityMiddleware>();
app.UseRateLimiter();

app.UseAntiforgery();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments(
            "/WorkoutImages",
            out var remainingPath))
    {
        var fileName = Path.GetFileName(remainingPath.Value);
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(
                remainingPath.Value,
                $"/{fileName}",
                StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var db = context.RequestServices.GetRequiredService<WorkoutDbContext>();
        var photoPath = $"/WorkoutImages/{fileName}";
        var ownsPhoto = await db.Exercises
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.PhotoPath == photoPath);

        if (!ownsPhoto)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
    }

    await next(context);
});

app.MapStaticAssets();

app.MapGet(
    "/WorkoutImages/{fileName}",
    async Task<IResult> (
        string fileName,
        ClaimsPrincipal principal,
        WorkoutDbContext db,
        IWebHostEnvironment environment) =>
    {
        var safeFileName = Path.GetFileName(fileName);
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId) ||
            !string.Equals(fileName, safeFileName, StringComparison.Ordinal))
        {
            return Results.NotFound();
        }

        var photoPath = $"/WorkoutImages/{safeFileName}";
        var ownsPhoto = await db.Exercises
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.PhotoPath == photoPath);

        if (!ownsPhoto)
            return Results.NotFound();

        var uploadPath = Path.Combine(
            environment.ContentRootPath,
            "App_Data",
            "WorkoutImages",
            safeFileName);

        var legacyPath = Path.Combine(
            environment.WebRootPath,
            "WorkoutImages",
            safeFileName);

        var filePath = File.Exists(uploadPath) ? uploadPath : legacyPath;

        if (!File.Exists(filePath))
            return Results.NotFound();

        var contentType = Path.GetExtension(safeFileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };

        return contentType == null
            ? Results.NotFound()
            : Results.File(filePath, contentType, enableRangeProcessing: true);
    })
    .RequireAuthorization();

app.MapGet(
    "/PublicationImages/{fileName}",
    async Task<IResult> (string fileName, WorkoutDbContext db, IWebHostEnvironment environment, TimeProvider clock) =>
    {
        var safeFileName = Path.GetFileName(fileName);
        if (fileName != safeFileName) return Results.NotFound();
        var imagePath = $"{PublicationImageStorage.PublicPrefix}{safeFileName}";
        if (!await db.InboxPublications.AsNoTracking().AnyAsync(
                x => x.ImagePath == imagePath && x.PublishedAtUtc <= clock.GetUtcNow().UtcDateTime))
            return Results.NotFound();
        var filePath = Path.Combine(environment.ContentRootPath, "App_Data", "PublicationImages", safeFileName);
        if (!File.Exists(filePath)) return Results.NotFound();
        var contentType = Path.GetExtension(safeFileName).ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", _ => null
        };
        return contentType is null ? Results.NotFound() : Results.File(filePath, contentType);
    })
    .AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGymPlannerApi();
app.MapAdminPublicationApi();
app.MapAdminOperationsApi();
app.MapTelegramSupportWebhook();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var db =
        scope.ServiceProvider
            .GetRequiredService<WorkoutDbContext>();

    db.Database.Migrate();

    DbSeeder.Seed(db);
    ExerciseLibrarySeeder.Seed(db);
    ExerciseSecondaryMuscleSeeder.Seed(db);

    LibraryValidator.Validate(db);

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync(ApplicationRoles.Admin))
    {
        var roleResult = await roleManager.CreateAsync(new IdentityRole(ApplicationRoles.Admin));
        if (!roleResult.Succeeded)
            throw new InvalidOperationException("Could not create the Admin role: " + string.Join(", ", roleResult.Errors.Select(x => x.Description)));
    }

    var grantAdminArgument = args.FirstOrDefault(x => x.StartsWith("--grant-admin=", StringComparison.OrdinalIgnoreCase));
    if (grantAdminArgument is not null)
    {
        var email = grantAdminArgument["--grant-admin=".Length..].Trim();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Existing user '{email}' was not found.");
        if (!await userManager.IsInRoleAsync(user, ApplicationRoles.Admin))
        {
            var grantResult = await userManager.AddToRoleAsync(user, ApplicationRoles.Admin);
            if (!grantResult.Succeeded)
                throw new InvalidOperationException("Could not grant Admin: " + string.Join(", ", grantResult.Errors.Select(x => x.Description)));
        }
        app.Logger.LogInformation("Admin role granted to existing account {Email}. Sign in again to refresh claims.", email);
        return;
    }
}

app.Run();

public partial class Program;
