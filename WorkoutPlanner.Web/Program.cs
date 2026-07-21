using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Components;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<WorkoutDbContext>(
    options =>
        options.UseSqlite(
            "Data Source=workoutplanner.db"));
builder.Services.AddScoped<HistoryService>();
builder.Services.AddScoped<ExerciseService>();
builder.Services.AddScoped<TrainingPlanService>();
builder.Services.AddScoped<TodayWorkoutService>();
builder.Services.AddScoped<WorkoutDayService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<ExerciseDefinitionService>();
builder.Services.AddScoped<ExerciseIndexService>();
builder.Services.AddScoped<SecondaryMuscleCoefficientService>();


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

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
