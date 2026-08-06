using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WorkoutPlanner.Web.Components;
using WorkoutPlanner.Web.Components.Onboarding;
using WorkoutPlanner.Web.Api;
using WorkoutPlanner.Web.Api.Security;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services;
using WorkoutPlanner.Web.Services.Auth;
using WorkoutPlanner.Web.Services.Onboarding;

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
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddIdentityApiEndpoints<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    })
    .AddEntityFrameworkStores<WorkoutDbContext>()
    .AddDefaultTokenProviders();
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
builder.Services.AddScoped<ITrainingPlanService, TrainingPlanService>();
builder.Services.AddScoped<IStarterPlanService, StarterPlanService>();
builder.Services.AddScoped<ITodayWorkoutService, TodayWorkoutService>();
builder.Services.AddScoped<IWorkoutDayService, WorkoutDayService>();
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<IExerciseDefinitionService, ExerciseDefinitionService>();
builder.Services.AddScoped<ExerciseIndexService>();
builder.Services.AddScoped<AppGuideCatalog>();
builder.Services.AddSingleton<CharacterAssetCatalog>();
builder.Services.AddScoped<AppGuidePracticeService>();
builder.Services.AddScoped<IOnboardingService, AppGuideService>();
builder.Services.AddScoped<IAppGuideCompletionStore, IdentityAppGuideCompletionStore>();


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

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGymPlannerApi();

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
}

app.Run();

public partial class Program;
